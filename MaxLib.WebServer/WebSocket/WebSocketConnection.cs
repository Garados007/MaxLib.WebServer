using System;
using Microsoft.Extensions.Logging;
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
        // Not cached per-type: this class is an arbitrary-subclass extension point, and this
        // logger lookup only happens on the (rare) network-error path below, so resolving the
        // concrete subclass's own logger category here costs nothing in practice.
        static readonly EventId WebSocketEventId = new(0, "WebSocket");

        public Stream NetworkStream { get; }
        private readonly SemaphoreSlim lockStream = new SemaphoreSlim(0, 1);

        public bool ReceivedCloseSignal { get; private set; }

        public bool SendCloseSignal { get; private set; }

        public DateTime LastPong { get; private set; }

        public event EventHandler? Closed;

        public event EventHandler? PongReceived;

        public WebSocketConnection(Stream networkStream)
        {
            NetworkStream = networkStream ?? throw new ArgumentNullException(nameof(networkStream));
        }

        public virtual void Dispose()
        {
            NetworkStream.Dispose();
            lockStream.Dispose();
            GC.SuppressFinalize(this);
        }

        public virtual async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await NetworkStream.DisposeAsync().ConfigureAwait(false);
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
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// This function is called after the handshake is finished
        /// </summary>
        public async Task HandshakeFinished()
        {
            var receiver = Task.Run(ReceiveLoop);

            // ping
            var pinger = Task.Run(async () =>
            {
                while (!SendCloseSignal)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                    if (SendCloseSignal)
                        break;
                    await SendFrame(new Frame
                    {
                        OpCode = OpCode.Ping
                    }).ConfigureAwait(false);
                }
            });

            await Task.WhenAll(receiver, pinger).ConfigureAwait(false);
            Closed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Reads and dispatches frames from <see cref="NetworkStream" /> until the connection
        /// closes or the stream ends. Releases the send-side lock as its first action, since
        /// sending is only meant to be possible once this loop is actually about to service the
        /// connection - call this at most once per connection, exactly what
        /// <see cref="HandshakeFinished" /> does. Exposed (<c>internal</c>) so tests can drive it
        /// directly without also running the (10-second-interval) ping loop.
        /// </summary>
        internal async Task ReceiveLoop()
        {
            lockStream.Release();
            var payloadQueue = new Queue<Memory<byte>>();
            OpCode code = OpCode.Binary;
            while (!ReceivedCloseSignal)
            {
                Frame? frame;
                try
                {
                    frame = await Frame.TryRead(NetworkStream, throwLargePayload: true).ConfigureAwait(false);
                }
                catch (TooLargePayloadException)
                {
                    await Close(CloseReason.TooBigMessage, $"Payload is larger then the allowed {int.MaxValue} bytes")
                        .ConfigureAwait(false);
                    return;
                }
                if (frame == null)
                    return;

                if (!frame.HasMaskingKey)
                {
                    // RFC 6455 §5.1: "A server MUST close the connection upon receiving a
                    // frame that is not masked."
                    await Close(CloseReason.ProtocolError, "Client frames must be masked")
                        .ConfigureAwait(false);
                    return;
                }
                frame.UnapplyMask();

                if (!frame.FinalFrame)
                {
                    // Per RFC 6455, only the first fragment of a message carries the real
                    // opcode; every later fragment is a Continuation, so only latch it once.
                    if (payloadQueue.Count == 0)
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
                        await ReceiveClose(reason, info).ConfigureAwait(false);
                        break;
                    case OpCode.Ping:
                        frame.OpCode = OpCode.Pong;
                        await SendFrame(frame).ConfigureAwait(false);
                        break;
                    case OpCode.Pong:
                        LastPong = DateTime.UtcNow;
                        _ = Task.Run(() => PongReceived?.Invoke(this, EventArgs.Empty));
                        break;
                    default:
                        if (payloadQueue.Count == 0)
                            await ReceivedFrame(frame).ConfigureAwait(false);
                        else
                        {
                            payloadQueue.Enqueue(frame.Payload);
                            var payload = TryReassembleFragmentedPayload(payloadQueue);
                            if (payload == null)
                            {
                                long maxSize = payloadQueue.Sum(x => (long)x.Length);
                                await Close(CloseReason.TooBigMessage,
                                    $"the payload of all frames add up to {maxSize}. Only {int.MaxValue} is allowed."
                                ).ConfigureAwait(false);
                                return;
                            }
                            frame.Payload = payload.Value;
                            frame.OpCode = code;
                            await ReceivedFrame(frame).ConfigureAwait(false);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Reassembles the queued fragments of the current message into one contiguous buffer. A
        /// <see cref="Memory{T}" /> can never represent more than <see cref="int.MaxValue" />
        /// bytes, so if the queued payloads add up to more than that, this returns <c>null</c>. The
        /// queue is left untouched in that case, so the caller can still inspect it (e.g. to report
        /// the total size back to the client) before discarding it.
        /// </summary>
        internal static Memory<byte>? TryReassembleFragmentedPayload(Queue<Memory<byte>> payloadQueue)
        {
            _ = payloadQueue ?? throw new ArgumentNullException(nameof(payloadQueue));
            long maxSize = payloadQueue.Sum(x => (long)x.Length);
            if (maxSize > int.MaxValue)
                return null;
            Memory<byte> payload = new byte[maxSize];
            int start = 0;
            while (payloadQueue.Count > 0)
            {
                var item = payloadQueue.Dequeue();
                item.CopyTo(payload.Slice(start, item.Length));
                start += item.Length;
            }
            return payload;
        }

        protected virtual async Task SendFrame(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            if (SendCloseSignal)
                return;
            await lockStream.WaitAsync().ConfigureAwait(false);
            if (frame.OpCode == OpCode.Close)
                SendCloseSignal = true;
            try
            {
                await frame.Write(NetworkStream).ConfigureAwait(false);
            }
            catch (IOException e)
            {
                if (frame.OpCode != OpCode.Ping && frame.OpCode != OpCode.Pong)
                    WebServerLog.LoggerFactory.CreateLogger(GetType())
                        .LogInformation(WebSocketEventId, e, "Unexpected network error: frame={OpCode}", frame.OpCode);
                var alreadyReceived = ReceivedCloseSignal;
                ReceivedCloseSignal = true;
                SendCloseSignal = true;
                if (!alreadyReceived)
                    await ReceiveClose(null, null).ConfigureAwait(false);
            }
            finally
            {
                lockStream.Release();
            }
        }

        protected abstract Task ReceiveClose(CloseReason? reason, string? info);

        protected abstract Task ReceivedFrame(Frame frame);
    }
}
