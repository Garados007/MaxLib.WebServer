using System.Linq;
using System.Threading;
using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.IO
{
    /// <summary>
    /// This reader is a combination out of <see cref="BinaryReader" /> and 
    /// <see cref="StreamReader" />. It allows to read text and binary data from the same
    /// stream without loosing any data.
    /// <br/>
    /// This reader is not thread-safe and is not intended to be used by multiple threads at the
    /// same time.
    /// </summary>
    public class NetworkReader : IDisposable, IAsyncDisposable
    {
        public Stream BaseStream { get; }

        private Encoding encoding;
        public Encoding Encoding
        {
            get => encoding;
            set
            {
                encoding = value ?? throw new ArgumentNullException(nameof(value));
                decoder = encoding.GetDecoder();
                charBufferCount = 0;
            }
        }

        Decoder decoder;
        readonly bool leaveOpen;
        /// <summary>
        /// The read buffer contains buffered bytes that is readed by this instance but not
        /// from the user.
        /// </summary>
        readonly Memory<byte> readBuffer;
        // readonly byte[] readBuffer;
        /// <summary>
        /// The position inside the <see cref="readBuffer" /> at which unreaded user bytes
        /// starts.
        /// </summary>
        int readBufferOffset;
        /// <summary>
        /// The number of buffered bytes in <see cref="readBuffer" /> that is already readed
        /// but not from the user accessed.
        /// </summary>
        int readBufferCount;
        /// <summary>
        /// The char buffer contains buffered chars that are already parsed from its byte
        /// representation. This chars are not consumed by the user right now. 
        /// <br />
        /// This char buffer contains normaly a small portion of <see cref="readBuffer" />
        /// decoded as chars. The ReadChar and ReadLine methods will consume first its
        /// chars from this buffer. The binary read methods will just discard the whole
        /// char buffer.
        /// <br />
        /// This char buffer is only created in the ReadLine methods.
        /// </summary>
        readonly char[] charBuffer;
        /// <summary>
        /// The offset in <see cref="charBuffer" /> at which unreaded chars starts.
        /// </summary>
        int charBufferOffset;
        /// <summary>
        /// The number of unreaded chars in <see cref="charBuffer" />.
        /// </summary>
        int charBufferCount;
        bool disposed = false;
        readonly int expectedCharBytes;

        public NetworkReader(Stream stream)
            : this(stream, Encoding.UTF8, false)
        { }

        public NetworkReader(Stream stream, Encoding encoding)
            : this(stream, encoding, false)
        {}

        public NetworkReader(Stream stream, Encoding? encoding = null, bool leaveOpen = false, 
            int bufferLength = 1024)
        {
            BaseStream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!BaseStream.CanRead)
                throw new ArgumentException("stream is not readable", nameof(stream));

            this.encoding = encoding ?? Encoding.UTF8;
            decoder = Encoding.GetDecoder();
            
            expectedCharBytes = Encoding.GetMaxByteCount(1);
            readBuffer = new byte[Math.Max(expectedCharBytes, bufferLength)];
            readBufferOffset = 0;
            readBufferCount = 0;
            charBuffer = new char[readBuffer.Length * expectedCharBytes];
            charBufferOffset = 0;
            charBufferCount = 0;

            this.leaveOpen = leaveOpen;
        }

        protected void RefillBuffer(int expectLength = 0)
        {
            // This is to fix a speed penality with memory streams:
            // An async read with Memory<> is 100 times fast than a sync one.
            // Maybe this is a bug with my installation or something else.

            RefillBufferAsync(CancellationToken.None, expectLength).AsTask().Wait();
            return;

#pragma warning disable CS0162 // Unreachable code detected

            if (expectLength <= 0)
                expectLength = expectedCharBytes;
            if (readBufferCount >= expectLength)
                return;
            // move the data to the left only if less then the half buffer is available
            if ((readBufferOffset << 1) > readBuffer.Length)
            {
                readBuffer.Span.Slice(readBufferOffset, readBufferCount)
                    .CopyTo(readBuffer.Span[ .. readBufferCount]);
                // Buffer.BlockCopy(readBuffer, readBufferOffset, readBuffer, 0, readBufferCount);
                readBufferOffset = 0;
            }
            // read until we have enough bytes to read a character
            var span = readBuffer.Span;
            do
            {
                int readed = BaseStream.Read(
                    span[(readBufferOffset + readBufferCount) .. ]
                );
                if (readed == 0)
                    return;
                readBufferCount += readed;
            }
            while (readBufferCount < expectLength);

#pragma warning restore CS0162 // Unreachable code detected
        }

        protected async ValueTask RefillBufferAsync(CancellationToken cancellationToken, 
            int expectLength = 0)
        {
            if (expectLength <= 0)
                expectLength = expectedCharBytes;
            if (readBufferCount >= expectLength)
                return;
            // move the data to the left only if less then the half buffer is available
            if ((readBufferOffset << 1) > readBuffer.Length && readBufferCount > 0)
            {
                readBuffer.Span.Slice(readBufferOffset, readBufferCount)
                    .CopyTo(readBuffer.Span[ .. readBufferCount]);
                readBufferOffset = 0;
            }
            // read until we have enough bytes to read a character
            do
            {
                int readed = await BaseStream.ReadAsync(
                    readBuffer[(readBufferOffset + readBufferCount) .. ],
                    cancellationToken
                ).ConfigureAwait(false);
                if (readed == 0)
                    return;
                readBufferCount += readed;
            }
            while (readBufferCount < expectLength);
        }

        int lastBytesUsed = 0;

        protected int RefillCharBuffer()
        {
            if (charBufferCount == 0)
            {
                readBufferOffset += lastBytesUsed;
                readBufferCount -= lastBytesUsed;
                RefillBuffer();
                charBufferOffset = 0;
                decoder.Convert(
                    readBuffer.Span.Slice(readBufferOffset, readBufferCount),
                    // new ReadOnlySpan<byte>(readBuffer, readBufferOffset, readBufferCount),
                    new Span<char>(charBuffer),
                    false,
                    out lastBytesUsed,
                    out charBufferCount,
                    out bool completed
                );
            }
            return charBufferCount;
        }

        protected async ValueTask<int> RefillCharBufferAsync(CancellationToken cancellationToken)
        {
            if (charBufferCount == 0)
            {
                readBufferOffset += lastBytesUsed;
                readBufferCount -= lastBytesUsed;
                await RefillBufferAsync(cancellationToken).ConfigureAwait(false);
                charBufferOffset = 0;
                decoder.Convert(
                    readBuffer.Span.Slice(readBufferOffset, readBufferCount),
                    // new ReadOnlySpan<byte>(readBuffer, readBufferOffset, readBufferCount),
                    new Span<char>(charBuffer),
                    false,
                    out lastBytesUsed,
                    out charBufferCount,
                    out bool completed
                );
            }
            return charBufferCount;
        }

        protected void MoveByteBufferFromCharBuffer(int fromCharIndex, int toCharIndex)
        {
            var byteCount = Encoding.GetByteCount(
                charBuffer,
                fromCharIndex,
                toCharIndex - fromCharIndex
            );
            readBufferOffset += byteCount;
            readBufferCount -= byteCount;
        }

        protected void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(null);
        }

        public void Dispose()
        {
            disposed = true;
            if (!leaveOpen)
                BaseStream.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            disposed = true;
            if (!leaveOpen)
                await BaseStream.DisposeAsync().ConfigureAwait(false);
        }

        public char? PeekChar()
        {
            ThrowIfDisposed();

            if (RefillCharBuffer() == 0)
                return null;
            
            return charBuffer[charBufferOffset];
        }

        public async ValueTask<char?> PeekCharAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (await RefillCharBufferAsync(cancellationToken).ConfigureAwait(false) == 0)
                return null;
            
            return charBuffer[charBufferOffset];
        }

        public char? ReadChar()
        {
            ThrowIfDisposed();

            if (RefillCharBuffer() == 0)
                return null;

            var result = charBuffer[charBufferOffset];
            ++charBufferOffset;
            return result;
        }

        public async ValueTask<char?> ReadCharAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (await RefillCharBufferAsync(cancellationToken).ConfigureAwait(false) == 0)
                return null;

            var result = charBuffer[charBufferOffset];
            ++charBufferOffset;
            return result;
        }
    
        public string? ReadLine()
        {
            ThrowIfDisposed();
            StringBuilder? sb = null;

            if (RefillCharBuffer() == 0)
                return null;

            do
            {
                int i = charBufferOffset;
                do
                {
                    char ch = charBuffer[i];
                    if (ch == '\r' || ch == '\n')
                    {
                        string s;
                        if (sb != null)
                        {
                            sb.Append(charBuffer, charBufferOffset, i - charBufferOffset);
                            s = sb.ToString();
                        }
                        else
                        {
                            s = new string(charBuffer, charBufferOffset, i - charBufferOffset);
                        }
                        charBufferCount -= i + 1 - charBufferOffset;
                        charBufferOffset = i + 1;
                        if (ch == '\r')
                        {
                            if (RefillCharBuffer() > 0)
                            {
                                if (charBuffer[charBufferOffset] == '\n')
                                {
                                    charBufferOffset++;
                                }
                            }
                        }
                        return s;
                    }
                    i++;
                }
                while (i < charBufferOffset + charBufferCount);

                sb ??= new StringBuilder(charBufferCount + 80);
                sb.Append(charBuffer, charBufferOffset, charBufferCount);
                charBufferCount = 0;
            }
            while (RefillCharBuffer() > 0);
            return sb.ToString();
        }

        public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            StringBuilder? sb = null;

            if (await RefillCharBufferAsync(cancellationToken).ConfigureAwait(false) == 0)
                return null;

            do
            {
                int i = charBufferOffset;
                do
                {
                    char ch = charBuffer[i];
                    if (ch == '\r' || ch == '\n')
                    {
                        string s;
                        if (sb != null)
                        {
                            sb.Append(charBuffer, charBufferOffset, i - charBufferOffset);
                            s = sb.ToString();
                        }
                        else
                        {
                            s = new string(charBuffer, charBufferOffset, i - charBufferOffset);
                        }
                        charBufferCount -= i + 1 - charBufferOffset;
                        charBufferOffset = i + 1;
                        if (ch == '\r')
                        {
                            if (await RefillCharBufferAsync(cancellationToken).ConfigureAwait(false) > 0)
                            {
                                if (charBuffer[charBufferOffset] == '\n')
                                {
                                    charBufferOffset++;
                                }
                            }
                        }
                        return s;
                    }
                    i++;
                }
                while (i < charBufferOffset + charBufferCount);

                sb ??= new StringBuilder(charBufferCount + 80);
                sb.Append(charBuffer, charBufferOffset, charBufferCount);
                charBufferCount = 0;
            }
            while (await RefillCharBufferAsync(cancellationToken).ConfigureAwait(false) > 0);
            return sb.ToString();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            _ = buffer ?? throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            // discard the whole char buffer
            if (charBufferOffset > 0)
            {
                MoveByteBufferFromCharBuffer(0, charBufferOffset);
            }
            charBufferCount = charBufferOffset = lastBytesUsed = 0;
            
            int length = Math.Min(count, readBufferCount);
            if (length > 0)
            {
                readBuffer.Span.Slice(readBufferOffset, length)
                    .CopyTo(new Span<byte>(buffer, offset, length));
                // Buffer.BlockCopy(readBuffer, readBufferOffset, buffer, offset, length);
                readBufferOffset += length;
                readBufferCount -= length;
            }

            if (count <= length)
                return length;
            
            return length + BaseStream.Read(buffer, offset + length, count - length);
        }
    
        public async ValueTask<int> ReadAsync(byte[] buffer, int offset, int count, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            _ = buffer ?? throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            // discard the whole char buffer
            if (charBufferOffset > 0)
            {
                MoveByteBufferFromCharBuffer(0, charBufferOffset);
            }
            charBufferCount = charBufferOffset = lastBytesUsed = 0;
            
            int length = Math.Min(count, readBufferCount);
            if (length > 0)
            {
                readBuffer.Span.Slice(readBufferOffset, length)
                    .CopyTo(new Span<byte>(buffer, offset, length));
                // Buffer.BlockCopy(readBuffer, readBufferOffset, buffer, offset, length);
                readBufferOffset += length;
                readBufferCount -= length;
            }

            if (count <= length)
                return length;
            
            return length + await BaseStream.ReadAsync(buffer, offset + length, count - length, cancellationToken).ConfigureAwait(false);
        }
    
        public async ValueTask<int> ReadAsync(Memory<byte> buffer, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // discard the whole char buffer
            if (charBufferOffset > 0)
                MoveByteBufferFromCharBuffer(0, charBufferOffset);
            charBufferCount = charBufferOffset = lastBytesUsed = 0;

            int length = Math.Min(buffer.Length, readBufferCount);
            if (length > 0)
            {
                readBuffer.Span.Slice(readBufferOffset, length)
                    .CopyTo(buffer.Span[ .. length]);
                readBufferOffset += length;
                readBufferCount -= length;
                buffer = buffer[length .. ];
            }

            if (buffer.Length == 0)
                return length;
            
            return length + await BaseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<byte[]> ReadBytesAsync(int count, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            
            var buffer = new byte[count];
            var readed = 0;
            while (readed < count)
            {
                var r = await ReadAsync(buffer, readed, count - readed, cancellationToken).ConfigureAwait(false);
                if (r == 0)
                    break;

                readed += r;
            }

            if (readed != count)
            {
                var copy = new byte[readed];
                Buffer.BlockCopy(buffer, 0, copy, 0, readed);
                buffer = copy;
            }

            return buffer;
        }
    
        public async ValueTask<Memory<byte>> ReadMemoryAsync(int count,
            CancellationToken cancellationToken = default)
        {
            var buffer = new Memory<byte>(new byte[count]);
            var readed = await ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            return buffer[0 .. readed];
        }

        public async ValueTask<int> ReadAsync(Stream buffer, int count, 
            CancellationToken cancellationToken = default, int blockSize = 0x1000)
        {
            ThrowIfDisposed();
            _ = buffer ?? throw new ArgumentNullException(nameof(buffer));
            if (!buffer.CanWrite)
                throw new ArgumentException("stream is not writable", nameof(buffer));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (blockSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(blockSize));
            
            var originalCount = count;
            var bytes = new byte[blockSize];
            while (count > 0)
            {
                int readed = await ReadAsync(bytes, 0, Math.Min(count, blockSize), cancellationToken).ConfigureAwait(false);
                if (readed == 0)
                    break;
                await buffer.WriteAsync(bytes, 0, readed, cancellationToken).ConfigureAwait(false);
                count -= readed;
            }
            
            return originalCount - count;
        }

        public ReadOnlyMemory<byte> ReadUntil(
            ReadOnlySpan<byte> marking
        )
        {
            if (marking.Length == 0)
                return ReadOnlyMemory<byte>.Empty;

            if (marking.Length * 2 > readBuffer.Length)
                throw new ArgumentException("marking is to large to fit the read buffer", nameof(marking));
            
            // discard the whole char buffer
            if (charBufferOffset > 0)
                MoveByteBufferFromCharBuffer(0, charBufferOffset);
            charBufferCount = charBufferOffset = lastBytesUsed = 0;

            // initialize a buffer where we can write completed data into
            using var stream = new MemoryStream();

            // loop until we found the signature
            int length;
            do
            {
                // ensure we have enough bytes in buffer
                RefillBuffer(marking.Length);
                if (readBufferCount < marking.Length)
                {
                    // there wasn't enough bytes at the end to fit the marking.
                    // we just read the rest and thats it
                    stream.Write(readBuffer.Span.Slice(readBufferOffset, readBufferCount));
                    readBufferOffset += readBufferCount;
                    readBufferCount = 0;
                    break;
                }

                int i = readBufferOffset;
                // move the check window until we found the pattern
                do
                {
                    if (readBuffer.Span[i ..].StartsWith(marking))
                        break;
                    i++;
                }
                while (i < readBufferOffset + readBufferCount);
                // add the data until the pattern to the stream
                stream.Write(readBuffer.Span[readBufferOffset .. i]);
                // move the index to the end
                length = i - readBufferOffset;
                readBufferOffset = i;
                readBufferCount -= length;
                // break if the length is 0
            }
            while (length > 0);

            // now extract the data from the buffer and return
            return stream.ToArray();
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReadUntilAsync(
            ReadOnlyMemory<byte> marking,
            CancellationToken cancellationToken = default
        )
        {
            if (marking.Length == 0)
                return marking;

            if (marking.Length * 2 > readBuffer.Length)
                throw new ArgumentException("marking is to large to fit the read buffer", nameof(marking));
            
            // discard the whole char buffer
            if (charBufferOffset > 0)
                MoveByteBufferFromCharBuffer(0, charBufferOffset);
            charBufferCount = charBufferOffset = lastBytesUsed = 0;

            // initialize a buffer where we can write completed data into
            using var stream = new MemoryStream();

            // loop until we found the signature
            int length;
            do
            {
                // ensure we have enough bytes in buffer
                await RefillBufferAsync(cancellationToken, marking.Length).ConfigureAwait(false);
                if (readBufferCount < marking.Length)
                {
                    // there wasn't enough bytes at the end to fit the marking.
                    // we just read the rest and thats it
                    stream.Write(readBuffer.Span.Slice(readBufferOffset, readBufferCount));
                    readBufferOffset += readBufferCount;
                    readBufferCount = 0;
                    break;
                }

                int i = readBufferOffset;
                // move the check window until we found the pattern
                do
                {
                    if (readBuffer.Span[i ..].StartsWith(marking.Span))
                        break;
                    i++;
                }
                while (i < readBufferOffset + readBufferCount);
                // add the data until the pattern to the stream
                stream.Write(readBuffer.Span[readBufferOffset .. i]);
                // move the index to the end
                length = i - readBufferOffset;
                readBufferOffset = i;
                readBufferCount -= length;
                // break if the length is 0
            }
            while (length > 0);

            // now extract the data from the buffer and return
            return stream.ToArray();
        }
    }
}