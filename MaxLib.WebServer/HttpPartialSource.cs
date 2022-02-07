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

        protected override async Task<long> WriteStreamInternal(Stream stream, long start, long? stop)
        {
            // start and stop will further restrict the limitations of this implementation.
            // In a future release this method will be simplified.

            var firstOutputByte = Start + start;
            var clientWindow = stop - start;
            var localWindow = Count - start;
            var window = localWindow == null ? clientWindow :
                clientWindow == null ? localWindow : Math.Min(localWindow.Value, clientWindow.Value);
            
            // throw first bytes away
            using var bin = new StreamBin();
            long total = await BaseSource.WriteStream(bin, 0, firstOutputByte)
                .ConfigureAwait(false);
            if (total < firstOutputByte)
                return 0;

            // read the rest of the source. This depends if the BaseSource is something we can seek
            // on. A future release will make the start-stop sequence obsolete therefore we no
            // longer need these special checks.
            if (BaseSource is HttpStreamDataSource sds && !sds.Stream.CanSeek)
                total = await BaseSource.WriteStream(stream, 0, window)
                    .ConfigureAwait(false);
            else total = await BaseSource.WriteStream(stream, firstOutputByte, firstOutputByte + window);

            // we are finished with reading the data
            return total;
        }

        private class StreamBin : Stream
        {
            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => true;

            public override long Length => 0;

            public override long Position { get => 0; set {} }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return 0;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return 0;
            }

            public override void SetLength(long value)
            {
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
            }
        }
    }
}