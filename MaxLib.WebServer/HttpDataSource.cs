using MaxLib.IO;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public abstract class HttpDataSource : IDisposable
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger(typeof(HttpDataSource));
        static readonly EventId TransformToStreamEventId = new(0, "Transform To Stream");

        /// <remarks>
        /// HttpDataSource.Dispose implementations must not throw.
        /// </remarks>
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
#pragma warning disable CA2000 // ownership passes to the returned FaultPropagatingStream, whose Dispose() disposes this in turn
            var buffered = new BufferedSinkStream();
#pragma warning restore CA2000
            var fault = new WriteFaultBox();
            _ = Task.Run(async () =>
            {
                try
                {
                    await dataSource.WriteStream(buffered).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // The reader of `buffered` must always observe completion, even if WriteStream fails before calling
                    // FinishWrite(), or it blocks forever. The captured exception is surfaced through FaultPropagatingStream.
                    fault.Exception = e;
                    try
                    {
                        logger.LogError(TransformToStreamEventId, e,
                            "WriteStream failed while transforming a HttpDataSource into a Stream");
                    }
                    catch (Exception)
                    {
                        // logging itself must never crash this background task
                    }
                }
                finally
                {
                    buffered.FinishWrite();
                }
            });
            return new FaultPropagatingStream(buffered, fault);
        }

        private sealed class WriteFaultBox
        {
            public volatile Exception? Exception;
        }

        /// <summary>
        /// Wraps the <see cref="BufferedSinkStream"/> returned by <see cref="TransformToStream"/>. Once the wrapped
        /// stream reports end-of-stream, rethrows the captured <c>WriteStream</c> failure (wrapped in an
        /// <see cref="IOException"/>) so a caller can tell an early failure from a legitimate end.
        /// </summary>
        private sealed class FaultPropagatingStream(Stream inner, WriteFaultBox fault) : Stream
        {
            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => inner.CanSeek;
            public override bool CanWrite => inner.CanWrite;
            public override long Length => inner.Length;
            public override long Position { get => inner.Position; set => inner.Position = value; }

            public override void Flush() => inner.Flush();

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = inner.Read(buffer, offset, count);
                if (read <= 0 && fault.Exception is Exception e)
                    throw new IOException(
                        "WriteStream failed while transforming a HttpDataSource into a Stream.", e);
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
            public override void SetLength(long value) => inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    inner.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
