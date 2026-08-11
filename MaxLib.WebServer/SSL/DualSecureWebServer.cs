using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;

#nullable enable 

namespace MaxLib.WebServer.SSL
{
    public class DualSecureWebServer : Server
    {
        public DualSecureWebServerSettings DualSettings => (DualSecureWebServerSettings)Settings;

        public DualSecureWebServer(DualSecureWebServerSettings settings) : base(settings)
        {
            WebServerLog.Add(ServerLogType.Information, GetType(), "StartUp", "The use of dual mode is critical");
        }

        protected override async Task ClientStartListen(HttpConnection connection)
        {
            if (connection.NetworkStream == null && connection.NetworkClient != null)
            {
                var peaker = new StreamPeaker(connection.NetworkClient.GetStream());
                var mark = peaker.FirstByte;
                if (mark != 0 && (mark < 32 || mark >= 127))
                {
                    var ssl = new SslStream(peaker, false);
                    connection.NetworkStream = ssl;
                    ssl.AuthenticateAsServer(
                        serverCertificate:          DualSettings.Certificate,
                        clientCertificateRequired:  false,
                        enabledSslProtocols:        SslProtocols.None,
                        checkCertificateRevocation: true
                        );
                    if (!ssl.IsAuthenticated)
                    {
                        ssl.Dispose();
                        connection.NetworkClient.Close();
                        AllConnections.Remove(connection);
                        return;
                    }
                }
                else connection.NetworkStream = peaker;
            }
            await base.ClientStartListen(connection).ConfigureAwait(false);
        }

        internal class StreamPeaker : Stream
        {
            public StreamPeaker(Stream baseStream)
            {
                BaseStream = baseStream ?? throw new ArgumentNullException("baseStream");
            }

            public Stream BaseStream { get; private set; }

            int firstByte = -1;
            bool firstByteRead = false;
            bool baseStreamAtEnd = false;

            public override bool CanRead => true;

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
                BaseStream.Flush();
            }

            // 0x00 is also returned once the underlying stream has ended before sending any
            // byte at all, so callers must not treat FirstByte alone as proof that a byte
            // was actually received - it is only meaningful together with HasFirstByte.
            public byte FirstByte
            {
                get
                {
                    if (firstByte == -1 && !baseStreamAtEnd)
                        GetFirstByte();
                    return baseStreamAtEnd ? (byte)0 : (byte)firstByte;
                }
            }

            public bool HasFirstByte
            {
                get
                {
                    if (firstByte == -1 && !baseStreamAtEnd)
                        GetFirstByte();
                    return !baseStreamAtEnd;
                }
            }

            void GetFirstByte()
            {
                var b = new byte[1];
                var read = BaseStream.Read(b, 0, 1);
                if (read == 0)
                {
                    baseStreamAtEnd = true;
                    return;
                }
                firstByte = b[0];
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException("buffer");
                if (offset < 0 || offset + count > buffer.Length)
                    throw new ArgumentOutOfRangeException("offset");
                if (count < 0)
                    throw new ArgumentOutOfRangeException("count");
                if (count == 0) return 0;
                if (firstByte == -1 && !baseStreamAtEnd) GetFirstByte();
                if (!firstByteRead)
                {
                    firstByteRead = true;
                    if (baseStreamAtEnd) return 0;
                    buffer[offset] = FirstByte;
                    if (count == 1) return 1;
                    var read = BaseStream.Read(buffer, offset + 1, count - 1);
                    return read + 1;
                }
                return BaseStream.Read(buffer, offset, count);
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
                BaseStream.Write(buffer, offset, count);
            }
        }
    }
}
