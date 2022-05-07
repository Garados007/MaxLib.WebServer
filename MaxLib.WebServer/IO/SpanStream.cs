using System;
using System.IO;

#nullable enable

namespace MaxLib.WebServer.IO
{
    /// <summary>
    /// This streams wraps a single <see cref="ReadOnlyMemory<byte>" />
    /// and give stream access to it.
    /// <br />
    /// This wrapper is readonly
    /// </summary>
    public class SpanStream : Stream
    {
        public ReadOnlyMemory<byte> Memory { get; }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => Memory.Length;

        int position = 0;
        public override long Position
        {
            get => position;
            set
            {
                if (value < 0 || value >= Memory.Length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                position = (int)value;
            }
        }


        /// <summary>
        /// This streams wraps a single <see cref="ReadOnlyMemory{T}" />
        /// and give stream access to it.
        /// <br />
        /// This wrapper is readonly
        /// </summary>
        public SpanStream(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _ = buffer ?? throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(buffer));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            count = Math.Min(count, Memory.Length - position);
            Memory.Span.Slice(position, count)
                .CopyTo(new Span<byte>(buffer, offset, count));
            position += count;
            return count;
        }

        public override int Read(Span<byte> buffer)
        {
            var count = Math.Min(Memory.Length - position, buffer.Length);
            Memory.Span.Slice(position, count)
                .CopyTo(buffer[.. count]);
            position += count;
            return count;
        }

        public override int ReadByte()
        {
            if (position < Memory.Length)
            {
                return Memory.Span[position++];
            }
            else return -1;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    return position = (int)Math.Max(0, Math.Min(Length, offset));
                case SeekOrigin.Current:
                    return position = (int)Math.Max(0, Math.Min(Length, position + offset));
                case SeekOrigin.End:
                    return position = (int)Math.Max(0, Math.Max(Length, Length + offset));
                default:
                    throw new NotSupportedException($"unsupported origin {origin}");
            }
        }

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}