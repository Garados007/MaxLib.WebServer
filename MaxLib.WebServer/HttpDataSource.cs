using MaxLib.IO;
using System;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public abstract class HttpDataSource : IDisposable
    {
        public abstract void Dispose();

        public abstract long? Length();

        private string mimeType = WebServer.MimeType.TextHtml;
        public virtual string MimeType
        {
            get => mimeType;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    mimeType = WebServer.MimeType.TextHtml;
                else mimeType = value;
            }
        }

        protected abstract Task<long> WriteStreamInternal(Stream stream, long start, long? stop);

        /// <summary>
        /// Write its content to <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">the stream to write the data into</param>
        /// <param name="start">the first own byte (inclusive) that should have been written</param>
        /// <param name="stop">the last own byte (excklusive) that should have been written or null to write all bytes till the end</param>
        /// <returns>the effective number of bytes written to the stream</returns>
        public async Task<long> WriteStream(Stream stream, long start, long? stop)
        {
            _ = stream ?? throw new ArgumentNullException(nameof(stream));
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
            var length = Length();
            if (length != null && start > length.Value)
                throw new ArgumentOutOfRangeException(nameof(start));
            if (stop != null && stop < start) throw new ArgumentOutOfRangeException(nameof(stop));
            return await WriteStreamInternal(stream, start, stop).ConfigureAwait(false);
        }

        /// <summary>
        /// Write its content to <paramref name="stream"/>. It will start at <see cref="RangeStart"/> 
        /// (inclusive) and write until <see cref="RangeEnd"/> (exclusive). If <see cref="RangeEnd"/> 
        /// is null it will write all bytes till the end.
        /// </summary>
        /// <param name="stream">the stream to write the data into</param>
        /// <returns>the effective number of bytes written to the stream</returns>
        public async Task<long> WriteStream(Stream stream)
#pragma warning disable CS0618
            => await WriteStream(stream ?? throw new ArgumentNullException(nameof(stream)), RangeStart, RangeEnd).ConfigureAwait(false);
#pragma warning restore CS0618

        private long rangeStart = 0;
        [Obsolete("HttpDataSource will change its Range behaviour")]
        public virtual long RangeStart
        {
            get => rangeStart;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(RangeStart));
                if (value > 0) TransferCompleteData = false;
                else if (rangeEnd == null) TransferCompleteData = true;
                rangeStart = value;
            }
        }

        private long? rangeEnd = null;
        [Obsolete("HttpDataSource will change its Range behaviour")]
        public virtual long? RangeEnd
        {
            get => rangeEnd;
            set
            {
                if (Length() == null && value != null)
                    throw new ArgumentOutOfRangeException(nameof(RangeEnd));
                if (value != null && value < 0) throw new ArgumentOutOfRangeException(nameof(RangeEnd));
                if (value != null)
                    TransferCompleteData = false;
                else if (rangeStart == 0) 
                    TransferCompleteData = true;
                rangeEnd = value;
            }
        }

        private bool transferCompleteData = true;
        [Obsolete("In a future release HttpDataSource will always transfer the complete data")]
        public virtual bool TransferCompleteData
        {
            get => transferCompleteData;
            set
            {
                if (transferCompleteData = value)
                {
                    rangeStart = 0;
                    rangeEnd = null;
                }
                else
                {
                    if (rangeEnd == null)
                        rangeEnd = Length();
                }
            }
        }

        public static Stream TransformToStream(HttpDataSource dataSource)
        {
            _ = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            var buffered = new BufferedSinkStream();
            _ = new Task(async () =>
            {
                await dataSource.WriteStream(buffered).ConfigureAwait(false);
                buffered.FinishWrite();
            });
            return buffered;
        }
    }
}
