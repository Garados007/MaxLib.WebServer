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

        protected abstract Task<long> WriteStreamInternal(Stream stream);

        /// <summary>
        /// Write the whole content to <paramref name="stream"/>. If you want to have a partial
        /// content use <see cref="HttpPartialSource" /> as a wrapper.
        /// </summary>
        /// <param name="stream">the stream to write the data into</param>
        /// <returns>the effective number of bytes written to the stream</returns>
        public async Task<long> WriteStream(Stream stream)
        {
            _ = stream ?? throw new ArgumentNullException(nameof(stream));
            return await WriteStreamInternal(stream).ConfigureAwait(false);
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
