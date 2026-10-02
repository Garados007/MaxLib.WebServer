using System;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    public class Frame
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<Frame>();
        static readonly EventId WebSocketEventId = new(0, "WebSocket");

        public bool FinalFrame { get; set; } = true;

        public OpCode OpCode { get; set; }

        public bool HasMaskingKey { get; set; }

        public Memory<byte> MaskingKey { get; } = new byte[4];

        public Memory<byte> Payload { get; set; }

        public string TextPayload
        {
            get => Encoding.UTF8.GetString(Payload.Span);
            set => Payload = Encoding.UTF8.GetBytes(value ?? throw new ArgumentNullException(nameof(value)));
        }

        public async Task Write(Stream output)
        {
            ArgumentNullException.ThrowIfNull(output);
            Memory<byte> buffer = new byte[8];
            buffer.Span[0] = (byte)((byte)OpCode | (FinalFrame ? 0x80 : 0x00));
            buffer.Span[1] = (byte)((HasMaskingKey ? 0x80 : 0x00) | (Payload.Length < 126 ? Payload.Length :
                (Payload.Length <= ushort.MaxValue ? 126 : 127)
            ));
            await output.WriteAsync(buffer[ .. 2]).ConfigureAwait(false);
            if (Payload.Length >= 126 && Payload.Length <= ushort.MaxValue)
            {
                ToNetworkByteOrder(BitConverter.GetBytes((ushort)Payload.Length), buffer.Span[0..2]);
                await output.WriteAsync(buffer[..2]).ConfigureAwait(false);
            }
            if (Payload.Length > ushort.MaxValue)
            {
                ToNetworkByteOrder(BitConverter.GetBytes((ulong)Payload.Length), buffer.Span);
                await output.WriteAsync(buffer).ConfigureAwait(false);
            }
            if (HasMaskingKey)
                await output.WriteAsync(MaskingKey).ConfigureAwait(false);
            if (Payload.Length > 0)
                await output.WriteAsync(Payload).ConfigureAwait(false);
        }

        /// <param name="input">the stream to read the frame from</param>
        /// <param name="throwLargePayload">
        /// throw <see cref="TooLargePayloadException" /> instead of returning null when the declared payload
        /// length exceeds <paramref name="maxPayloadSize" /> or <see cref="int.MaxValue" />
        /// </param>
        /// <param name="maxPayloadSize">
        /// reject the frame before allocating or reading its payload once its declared length exceeds this value;
        /// a negative value (the default) only enforces the <see cref="int.MaxValue" /> cap. Pass
        /// <see cref="WebSocketConnection.MaxMessageSize" /> here too, as unfragmented frames bypass reassembly.
        /// </param>
        public static async Task<Frame?> TryRead(Stream input, bool throwLargePayload = false,
            long maxPayloadSize = -1)
        {
            ArgumentNullException.ThrowIfNull(input);
            try
            {
                // ReadAsync may return fewer bytes than requested; ReadExactlyAsync loops until the buffer is full
                // (or throws EndOfStreamException)
                Memory<byte> buffer = new byte[8];
                await input.ReadExactlyAsync(buffer[0..2]).ConfigureAwait(false);
                var frame = new Frame
                {
                    FinalFrame = (buffer.Span[0] & 0x80) == 0x80,
                    OpCode = (OpCode)(buffer.Span[0] & 0x0f),
                    HasMaskingKey = (buffer.Span[1] & 0x80) == 0x80,
                };
                var lengthIndicator = buffer.Span[1] & 0x7f;
                ulong length = (ulong)lengthIndicator;
                if (lengthIndicator == 126)
                {
                    await input.ReadExactlyAsync(buffer[0..2]).ConfigureAwait(false);
                    ToLocalByteOrder(buffer.Span[..2]);
                    length = BitConverter.ToUInt16(buffer.Span[..2]);
                }
                if (lengthIndicator == 127)
                {
                    await input.ReadExactlyAsync(buffer).ConfigureAwait(false);
                    ToLocalByteOrder(buffer.Span);
                    length = BitConverter.ToUInt64(buffer.Span);
                }
                if (length > int.MaxValue || (maxPayloadSize >= 0 && length > (ulong)maxPayloadSize))
                {
                    if (throwLargePayload)
                        throw new TooLargePayloadException();
                    else return null;
                }

                if (frame.HasMaskingKey)
                {
                    await input.ReadExactlyAsync(buffer[..4]).ConfigureAwait(false);
                    buffer[..4].CopyTo(frame.MaskingKey);
                }

                frame.Payload = new byte[(int)length];
                await input.ReadExactlyAsync(frame.Payload).ConfigureAwait(false);

                return frame;
            }
            catch (TooLargePayloadException)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogInformation(WebSocketEventId, e, "Cannot read frame");
                return null;
            }
        }

        public static void ToNetworkByteOrder(ReadOnlySpan<byte> input, Span<byte> buffer)
        {
            if (input.Length != buffer.Length)
                throw new InvalidOperationException();
            if (BitConverter.IsLittleEndian)
                for (int i = 0; i < input.Length; ++i)
                    buffer[input.Length - i - 1] = input[i];
            else input.CopyTo(buffer);
        }

        public static void ToLocalByteOrder(Span<byte> buffer)
        {
            if (BitConverter.IsLittleEndian)
                buffer.Reverse();
        }

        protected static void ToBytes(ushort value, Span<byte> buffer)
        {
            var result = BitConverter.GetBytes(value);
            if (result.Length > buffer.Length)
                throw new InvalidOperationException();
            if (BitConverter.IsLittleEndian)
                for (int i = 0; i < result.Length; ++i)
                    buffer[result.Length - i - 1] = result[i];
            else result.CopyTo(buffer);
        }

        public void ApplyMask()
        {
            if (HasMaskingKey)
                return;
            // RFC 6455 §5.3: the masking key must be unpredictable and differ per frame
            RandomNumberGenerator.Fill(MaskingKey.Span);
            var span = Payload.Span;
            var mask = MaskingKey.Span;
            for (int i = 0; i < span.Length; ++i)
                span[i] ^= mask[i & 0x3];
            HasMaskingKey = true;
        }

        public void UnapplyMask()
        {
            if (!HasMaskingKey)
                return;
            var span = Payload.Span;
            var mask = MaskingKey.Span;
            for (int i = 0; i < span.Length; ++i)
                span[i] ^= mask[i & 0x3];
            HasMaskingKey = false;
        }
    }

    public enum OpCode : byte
    {
        Continuation = 0x0,
        Text = 0x1,
        Binary = 0x2,
        Close = 0x8,
        Ping = 0x9,
        Pong = 0xa,
    }
}
