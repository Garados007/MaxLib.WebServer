using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    public class Frame
    {
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
            Memory<byte> buffer = new byte[8];
            buffer.Span[0] = (byte)((byte)OpCode | (FinalFrame ? 0x80 : 0x00));
            buffer.Span[1] = (byte)(Payload.Length < 126 ? Payload.Length : 
                (Payload.Length <= ushort.MaxValue ? 126 : 127)
            );
            await output.WriteAsync(buffer[ .. 2]);
            if (Payload.Length >= 126 && Payload.Length <= ushort.MaxValue)
            {
                ToNetworkByteOrder(BitConverter.GetBytes((ushort)Payload.Length), buffer.Span[0..2]);
                await output.WriteAsync(buffer[..2]);
            }
            if (Payload.Length > ushort.MaxValue)
            {
                ToNetworkByteOrder(BitConverter.GetBytes((ulong)Payload.Length), buffer.Span);
                await output.WriteAsync(buffer);
            }
            if (HasMaskingKey)
                await output.WriteAsync(MaskingKey);
            await output.WriteAsync(Payload);
        }

        public static async Task<Frame?> TryRead(Stream input, bool throwLargePayload = false)
        {
            try
            {
                Memory<byte> buffer = new byte[8];
                if (await input.ReadAsync(buffer[0..2]) != 2)
                    return null;
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
                    if (await input.ReadAsync(buffer[0..2]) != 2)
                        return null;
                    ToLocalByteOrder(buffer.Span[..2]);
                    length = BitConverter.ToUInt16(buffer.Span[..2]);
                }
                if (lengthIndicator == 127)
                {
                    if (await input.ReadAsync(buffer) != 8)
                        return null;
                    ToLocalByteOrder(buffer.Span);
                    length = BitConverter.ToUInt64(buffer.Span);
                }
                if (length > int.MaxValue)
                {
                    if (throwLargePayload)
                        throw new TooLargePayloadException();
                    else return null;
                }
                
                if (frame.HasMaskingKey)
                {
                    if (await input.ReadAsync(buffer[..4]) != 4)
                        return null;
                    buffer[..4].CopyTo(frame.MaskingKey);
                }

                frame.Payload = new byte[(int)length];
                if (await input.ReadAsync(frame.Payload) != frame.Payload.Length)
                    return null;

                return frame;
            }
            catch (TooLargePayloadException)
            {
                throw;
            }
            catch (Exception e)
            {
                WebServerLog.Add(ServerLogType.Information, typeof(Frame), "WebSocket", $"cannot read frame: {e}");
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

        protected void ToBytes(ushort value, Span<byte> buffer)
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
