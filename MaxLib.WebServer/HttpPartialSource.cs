using System;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    /// <summary>
    /// This data source will throw the first n bytes away and then return the data of the contained
    /// data source.
    /// </summary>
    public class HttpPartialSource : HttpDataSource
    {
        /// <summary>
        /// The contained data source which data should be trimmed
        /// </summary>
        public HttpDataSource BaseSource { get; }

        /// <summary>
        /// The number of first bytes that should be thrown away from the <see cref="BaseSource" />
        /// if someone want's to read data from this stream.
        /// </summary>
        public long Start { get; }

        /// <summary>
        /// The number of bytes that are allowed to read from the contained <see cref="BaseSource"
        /// />. If this value is null this stream will return all remaining bytes from the contained
        /// stream.
        /// </summary>
        public long? Count { get; }

        public HttpPartialSource(HttpDataSource dataSource, long start, long? count)
        {
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start));
            if (count != null && count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            BaseSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            Start = start;
            Count = count;

            // optimize this constructor if you use nested partial sources
            if (dataSource is HttpPartialSource partial)
            {
                BaseSource = partial.BaseSource;
                Start += partial.Start;
                if (Count != null && partial.Count != null)
                    Count = Math.Min(Count.Value, partial.Count.Value - Start);
                else
                {
                    Count ??= partial.Count - Start;
                }
            }
        }

        public override void Dispose()
        {
            BaseSource.Dispose();
        }

        public override long? Length()
        {
            var baseLength = BaseSource.Length();
            var availableBaseLength = baseLength == null ? null : baseLength - Start;
            return Count == null ? availableBaseLength :
                availableBaseLength == null ? Count : Math.Min(Count.Value, availableBaseLength.Value);
        }

        protected override async Task<long> WriteStreamInternal(Stream stream)
        {
            // optimize if stream based
            if (BaseSource is HttpStreamDataSource streamDataSource)
            {
                var window = new StreamWindow(stream, 0, Count);
                return await streamDataSource.WriteStream(window, Start, Count);
            }
            else
            {
                var window = new StreamWindow(stream, Start, Count);
                return await BaseSource.WriteStream(window);
            }
        }

        private class StreamWindow : Stream
        {
            public Stream Target { get; }

            public long Start { get; private set; }

            public long? Count { get; private set; }

            public StreamWindow(Stream target, long start, long? count)
            {
                Target = target; 
                Start = start;
                Count = count;
            }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
                Target.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                // skip bytes if available
                if (Start > 0)
                {
                    var skip = (int)Math.Min(Start, count);
                    Start -= skip;
                    offset += skip;
                    count -= skip;
                }
                // trim count if necessary
                if (Start == 0 && Count != null)
                {
                    var usable = (int)Math.Min(Count.Value, count);
                    count = usable;
                    Count = Count.Value - usable;
                }
                // perform write operation
                if (count > 0)
                    Target.Write(buffer, offset, count);
            }
        }
    }
}