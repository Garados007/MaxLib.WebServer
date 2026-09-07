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

        /// <summary>
        /// RFC 6455 §5.5: control frames (<see cref="OpCode.Close" />/<see cref="OpCode.Ping" />/
        /// <see cref="OpCode.Pong" />) MUST have a payload of at most this many bytes, and MUST
        /// NOT be fragmented.
        /// </summary>
        private const int MaxControlFramePayloadSize = 125;

        public Stream NetworkStream { get; }
        private readonly SemaphoreSlim lockStream = new SemaphoreSlim(0, 1);

        public bool ReceivedCloseSignal { get; private set; }

        public bool SendCloseSignal { get; private set; }

        public DateTime LastPong { get; private set; }

        /// <summary>
        /// The maximum total size, in bytes, of the fragments accumulated so far while
        /// reassembling a fragmented message. Checked after every fragment - not only once the
        /// message is complete - so a client that never finalizes a message (or does so very
        /// slowly) can't grow the accumulation queue indefinitely. Exceeding it closes the
        /// connection with <see cref="CloseReason.TooBigMessage" /> immediately. Defaults to
        /// 100 MB; a negative value disables this check (not recommended - see also
        /// <see cref="MaxMessageFragments" />).
        /// </summary>
        public long MaxMessageSize { get; set; } = 100_000_000;

        /// <summary>
        /// The maximum number of fragments accumulated while reassembling a message, checked
        /// alongside <see cref="MaxMessageSize" />. A size limit alone doesn't bound the
        /// per-fragment bookkeeping overhead itself - many empty or near-empty fragments still
        /// cost one queued entry each. Defaults to 10,000; a negative value disables this check.
        /// </summary>
        public int MaxMessageFragments { get; set; } = 10_000;

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
            // RFC 6455 §5.5: control frame payloads (Close included) must not exceed 125 bytes;
            // truncate rather than send a non-compliant frame.
            if (size > MaxControlFramePayloadSize)
                size = MaxControlFramePayloadSize;
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
            long accumulatedSize = 0;
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

                if (IsControlFrame(frame.OpCode) &&
                    (!frame.FinalFrame || frame.Payload.Length > MaxControlFramePayloadSize))
                {
                    // RFC 6455 §5.4/§5.5: control frames must not be fragmented and their
                    // payload must not exceed 125 bytes.
                    await Close(CloseReason.ProtocolError,
                        "Control frames must not be fragmented and must not exceed 125 bytes"
                    ).ConfigureAwait(false);
                    return;
                }

                if (!frame.FinalFrame)
                {
                    // Per RFC 6455, only the first fragment of a message carries the real
                    // opcode; every later fragment is a Continuation, so only latch it once.
                    if (payloadQueue.Count == 0)
                    {
                        code = frame.OpCode;
                        accumulatedSize = 0;
                    }
                    payloadQueue.Enqueue(frame.Payload);
                    accumulatedSize += frame.Payload.Length;
                    if (await ExceedsConfiguredMessageLimits(payloadQueue.Count, accumulatedSize).ConfigureAwait(false))
                        return;
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
                            accumulatedSize += frame.Payload.Length;
                            if (await ExceedsConfiguredMessageLimits(payloadQueue.Count, accumulatedSize).ConfigureAwait(false))
                                return;
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

        private static bool IsControlFrame(OpCode opCode)
            => opCode is OpCode.Close or OpCode.Ping or OpCode.Pong;

        /// <summary>
        /// Checks the running fragment count/size accumulated so far for the message currently
        /// being reassembled against <see cref="MaxMessageFragments" />/<see
        /// cref="MaxMessageSize" />, sending a <see cref="CloseReason.TooBigMessage" /> close
        /// frame and returning <c>true</c> if either is exceeded - the caller must stop
        /// processing this connection in that case, rather than continuing to buffer an
        /// unbounded amount of data until (or unless) the client ever finalizes the message.
        /// </summary>
        private async Task<bool> ExceedsConfiguredMessageLimits(int fragmentCount, long accumulatedSize)
        {
            if ((MaxMessageFragments >= 0 && fragmentCount > MaxMessageFragments) ||
                (MaxMessageSize >= 0 && accumulatedSize > MaxMessageSize))
            {
                await Close(CloseReason.TooBigMessage,
                    $"the message being reassembled exceeds the configured limit " +
                    $"({MaxMessageFragments} fragments / {MaxMessageSize} bytes)."
                ).ConfigureAwait(false);
                return true;
            }
            return false;
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
