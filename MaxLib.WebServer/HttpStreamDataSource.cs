using MaxLib.IO;
using System;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpStreamDataSource : HttpDataSource
    {
        public Stream Stream { get; }

        public HttpStreamDataSource(Stream stream)
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException(
                    "stream is expected to be seekable. use HttpChunkedStream instead",
                    nameof(stream)
                );
        }

        public override void Dispose()
            => Stream.Dispose();

        public override long? Length()
            => Stream.Length;

        protected override Task<long> WriteStreamInternal(Stream stream)
        {
            return WriteStream(stream, 0, null);
        }

        public async Task<long> WriteStream(Stream stream, long offset, long? count)
        {
            if (Stream.CanSeek)
                Stream.Position = offset;
            long total = 0;
            Memory<byte> buffer = new byte[0x8000];
            try
            {
                int read;
                int job = count == null ? buffer.Length : (int)Math.Min(buffer.Length, count.Value - total);
                while ((read = await Stream.ReadAsync(buffer[..job])) > 0)
                {
                    await stream.WriteAsync(buffer[0..read]);
                    total += read;
                }
            }
            catch (IOException)
            {
                WebServerLog.Add(ServerLogType.Information, GetType(), "Send", "Connection closed by remote Host");
            }
            return total;
        }
    }
}
