﻿using System;
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

        protected override async Task<long> WriteStreamInternal(Stream stream, long start, long? stop)
        {
            long total = 0;
            int readed;
            byte[] buffer = new byte[ReadBufferLength];
            do
            {
                readed = await BaseStream.ReadAsync(
                    buffer, 
                    0, 
                    (int)Math.Min(buffer.Length, start - total)
                ).ConfigureAwait(false);
                total += readed;
            }
            while (total < start && readed > 0);
            if (readed == 0 && start > 0)
                return 0;
            var ascii = Encoding.ASCII;
            var nl = ascii.GetBytes("\r\n");
            do
            {
                var read = stop == null
                    ? buffer.Length
                    : (int)Math.Min(buffer.Length, stop.Value - total);
                readed = await BaseStream.ReadAsync(buffer, 0, read).ConfigureAwait(false);
                if (readed <= 0)
                    return total - start;
                var length = ascii.GetBytes(readed.ToString("X"));
                try
                {
                    await stream.WriteAsync(length, 0, length.Length).ConfigureAwait(false);
                    await stream.WriteAsync(nl, 0, nl.Length).ConfigureAwait(false);
                    await stream.WriteAsync(buffer, 0, readed).ConfigureAwait(false);
                    await stream.WriteAsync(nl, 0, nl.Length).ConfigureAwait(false);
                    total += readed;
                    await stream.FlushAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    WebServerLog.Add(ServerLogType.Information, GetType(), "write", "connection closed");
                    return total;
                }
            }
            while (readed > 0);
            return total - start;
        }

        private async Task<int> ReadNumber(Stream stream, byte[] buffer)
        {
            int offset = 0;
            var byteBuffer = new byte[1];
            while (true)
            {
                int readed = await stream.ReadAsync(byteBuffer, 0, 1).ConfigureAwait(false);
                if (readed == 0)
                    return offset;
                if (byteBuffer[0] == '\r' || byteBuffer[0] == '\n')
                {
                    if (byteBuffer[0] == '\r')
                        await stream.ReadAsync(byteBuffer, 0, 1).ConfigureAwait(false);
                    return offset;
                }
                buffer[offset] = byteBuffer[0];
                offset++;
            }
        }
    }
}
