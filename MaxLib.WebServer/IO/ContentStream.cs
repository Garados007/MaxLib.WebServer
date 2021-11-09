using System.IO;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.IO
{
    /// <summary>
    /// This stream maps a single <see cref="NetworkReader" /> instance and allows to read a
    /// specific length of data from it.
    /// </summary>
    public class ContentStream : Stream
    {
        private NetworkReader reader;

        /// <summary>
        /// The count of bytes that are already read.
        /// </summary>
        public long ReadData { get; private set; }
        /// <summary>
        /// The maximum number of bytes that are allowed to read
        /// </summary>
        public long FullLength { get; }
        /// <summary>
        /// The number of bytes that is unread
        /// </summary>
        public long UnreadData => FullLength - ReadData;

        public ContentStream(NetworkReader reader, long maximum)
        {
            this.reader = reader;
            FullLength = maximum;
        }

        public override bool CanRead => UnreadData > 0;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => FullLength;

        public override long Position 
        {
            get => ReadData; 
            set => throw new InvalidOperationException("cannot seek on this stream"); 
        }

        public override void Flush()
        {
        }

        /// <summary>
        /// This will discard any unread data and release the underlying <see cref="NetworkReader"
        /// />.
        /// </summary>
        public virtual void Discard()
        {
            var buffer = new byte[64 * 1024];
            while (UnreadData > 0)
            {
                var length = reader.Read(buffer, 0, (int)Math.Min(buffer.Length, UnreadData));
                ReadData += length;
            }
        }

        /// <summary>
        /// This will discard any unread data and release the underlying <see cref="NetworkReader"
        /// />.
        /// </summary>
        public virtual Task DiscardAsync()
            => DiscardAsync(CancellationToken.None);

        /// <summary>
        /// This will discard any unread data and release the underlying <see cref="NetworkReader"
        /// />.
        /// </summary>
        public virtual async Task DiscardAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var buffer = new byte[64 * 1024];
            while (UnreadData > 0)
            {
                var length = await reader.ReadAsync(
                    buffer, 
                    0, 
                    (int)Math.Min(buffer.Length, UnreadData), 
                    cancellationToken
                )
                    .ConfigureAwait(false);
                ReadData += length;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || count + offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count > UnreadData)
                count = (int)UnreadData;
            var length = reader.Read(buffer, offset, count);
            ReadData += length;
            return length;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || count + offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count > UnreadData)
                count = (int)UnreadData;
            var length = await reader.ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
            ReadData += length;
            return length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException("Cannot seek on this stream");
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException("Cannot set length on this stream");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Cannot write on this stream");
        }

        protected override void Dispose(bool disposing)
        {
            Discard();
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await DiscardAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}