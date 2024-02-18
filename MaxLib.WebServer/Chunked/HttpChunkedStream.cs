using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Chunked
{
    [Serializable]
    public class HttpChunkedStream : HttpDataSource
    {
        public HttpChunkedStream(Stream baseStream, int readBufferLength = 0x8000)
        {
            BaseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            if (readBufferLength <= 0) 
                throw new ArgumentOutOfRangeException(nameof(readBufferLength));
            ReadBufferLength = readBufferLength;
        }

        public Stream BaseStream { get; }

        public int ReadBufferLength { get; }

        public override long? Length() => null;

        public override void Dispose()
        {
            BaseStream.Dispose();
        }

        protected override async Task<long> WriteStreamInternal(Stream stream)
        {
            long total = 0;
            int read;
            Memory<byte> buffer = new byte[ReadBufferLength];
            var ascii = Encoding.ASCII;
            ReadOnlyMemory<byte> nl = ascii.GetBytes("\r\n");
            do
            {
                read = await BaseStream.ReadAsync(buffer).ConfigureAwait(false);
                if (read <= 0)
                    return total;
                ReadOnlyMemory<byte> length = ascii.GetBytes(read.ToString("X"));
                try
                {
                    await stream.WriteAsync(length).ConfigureAwait(false);
                    await stream.WriteAsync(nl).ConfigureAwait(false);
                    await stream.WriteAsync(buffer[0..read]).ConfigureAwait(false);
                    await stream.WriteAsync(nl).ConfigureAwait(false);
                    total += read;
                    await stream.FlushAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    WebServerLog.Add(ServerLogType.Information, GetType(), "write", "connection closed");
                    return total;
                }
            }
            while (read > 0);
            return total;
        }
    }
}
