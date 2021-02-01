using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.Text;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    public abstract class WebSocketConnection : IDisposable, IAsyncDisposable
    {
        public Stream NetworkStream { get; }
        private readonly SemaphoreSlim lockStream = new SemaphoreSlim(0, 1);

        public bool ReceivedCloseSignal { get; private set; }

        public bool SendCloseSignal { get; private set; }

        public DateTime LastPong { get; private set; }

        public event EventHandler? Closed;

        public WebSocketConnection(Stream networkStream)
        {
            NetworkStream = networkStream ?? throw new ArgumentNullException(nameof(networkStream));
        }

        public virtual void Dispose()
        {
            NetworkStream.Dispose();
            lockStream.Dispose();
        }

        public virtual async ValueTask DisposeAsync()
        {
            await NetworkStream.DisposeAsync();
            lockStream.Dispose();
        }

        public async Task Close(CloseReason reason = CloseReason.NormalClose, string? info = null)
        {
            Memory<byte> payload = new byte[2 + (info == null ? 0 : Encoding.UTF8.GetByteCount(info))];
            Frame.ToNetworkByteOrder(BitConverter.GetBytes((ushort)reason), payload.Span[..2]);
            int size = payload.Length;
            if (info != null)
                size = 2 + Encoding.UTF8.GetBytes(info, payload.Span[2..]);
            await SendFrame(new Frame
            {
                OpCode = OpCode.Close,
                Payload = payload[..size],
            });
        }

        /// <summary>
        /// This function is called after the handshake is finished
        /// </summary>
        public async Task HandshakeFinished()
        {
            lockStream.Release();
            // receiving end
            var receiver = Task.Run(async () =>
            {
                var payloadQueue = new Queue<Memory<byte>>();
                OpCode code = OpCode.Binary;
                while (!ReceivedCloseSignal)
                {
                    Frame? frame;
                    try
                    {
                        frame = await Frame.TryRead(NetworkStream);
                    }
                    catch (TooLargePayloadException)
                    {
                        await Close(CloseReason.TooBigMessage, $"Payload is larger then the allowed {int.MaxValue} bytes");
                        return;
                    }
                    if (frame == null)
                        return;

                    frame.UnapplyMask();

                    if (!frame.FinalFrame)
                    {
                        code = frame.OpCode;
                        payloadQueue.Enqueue(frame.Payload);
                        continue;
                    }

                    switch (frame.OpCode)
                    {
                        case OpCode.Close:
                            CloseReason? reason = null;
                            string? info = null;
                            if (frame.Payload.Length >= 2)
                            {
                                Frame.ToLocalByteOrder(frame.Payload.Span[0..2]);
                                reason = (CloseReason)BitConverter.ToUInt16(frame.Payload.Span[0..2]);
                            }
                            if (frame.Payload.Length > 2)
                            {
                                info = Encoding.UTF8.GetString(frame.Payload.Span[2..]);
                            }
                            ReceivedCloseSignal = true;
                            await ReceiveClose(reason, info);
                            break;
                        case OpCode.Ping:
                            frame.OpCode = OpCode.Pong;
                            await SendFrame(frame);
                            break;
                        case OpCode.Pong:
                            LastPong = DateTime.UtcNow;
                            break;
                        default:
                            if (payloadQueue.Count == 0)
                                await ReceivedFrame(frame);
                            else
                            {
                                payloadQueue.Enqueue(frame.Payload);
                                long maxSize = payloadQueue.Sum(x => (long)x.Length);
                                if (maxSize > int.MaxValue)
                                {
                                    await Close(CloseReason.TooBigMessage, 
                                        $"the payload of all frames add up to {maxSize}. Only {int.MaxValue} is allowed."
                                    );
                                }
                                Memory<byte> payload = new byte[maxSize];
                                int start = 0;
                                while (payloadQueue.Count > 0)
                                {
                                    var item = payloadQueue.Dequeue();
                                    item.CopyTo(payload.Slice(start, item.Length));
                                    start += item.Length;
                                }
                                frame.Payload = payload;
                                frame.OpCode = code;
                                await ReceivedFrame(frame);
                            }
                            break;
                    }
                }
            });

            // ping
            var pinger = Task.Run(async () =>
            {
                while (!SendCloseSignal)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    await SendFrame(new Frame
                    {
                        OpCode = OpCode.Ping
                    });
                }
            });

            await Task.WhenAll(receiver, pinger);
            Closed?.Invoke(this, EventArgs.Empty);
        }

        protected virtual async Task SendFrame(Frame frame)
        {
            if (SendCloseSignal)
                return;
            await lockStream.WaitAsync();
            if (frame.OpCode == OpCode.Close)
                SendCloseSignal = true;
            try 
            { 
                await frame.Write(NetworkStream); 
                lockStream.Release();
            }
            catch (IOException e)
            {
                WebServerLog.Add(ServerLogType.Information, GetType(), "WebSocket", $"Unexpected network error: {e}");
                var alreadyReceived = ReceivedCloseSignal;
                ReceivedCloseSignal = true;
                SendCloseSignal = true;
                if (!alreadyReceived)
                    await ReceiveClose(null, null);
            }
        }

        protected abstract Task ReceiveClose(CloseReason? reason, string? info);

        protected abstract Task ReceivedFrame(Frame frame);
    }
}
