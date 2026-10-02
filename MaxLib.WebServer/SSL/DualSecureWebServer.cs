using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#nullable enable

namespace MaxLib.WebServer.SSL
{
    /// <remarks>
    /// The configured logging provider must not throw while rendering exceptions; the server does not
    /// guard its log calls.
    /// </remarks>
    public class DualSecureWebServer : Server
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<DualSecureWebServer>();
        static readonly EventId StartUpEventId = new(0, "StartUp");
        static readonly EventId HandshakeEventId = new(0, "Handshake");

        public DualSecureWebServerSettings DualSettings => (DualSecureWebServerSettings)Settings;

        public DualSecureWebServer(DualSecureWebServerSettings settings) : base(settings)
        {
            logger.LogInformation(StartUpEventId, "The use of dual mode is critical");
        }

        protected override async Task ClientStartListen(HttpConnection connection)
        {
            ArgumentNullException.ThrowIfNull(connection);
            if (connection.NetworkStream == null && connection.NetworkClient != null)
            {
                var peaker = new StreamPeaker(connection.NetworkClient.GetStream());
                try
                {
                    using var peekTimeout = DualSettings.HandshakeTimeout > TimeSpan.Zero
                        ? new CancellationTokenSource(DualSettings.HandshakeTimeout)
                        : null;
                    await peaker.EnsureFirstByteAsync(peekTimeout?.Token ?? CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // a client that never sends a byte would otherwise occupy this connection indefinitely
                    await peaker.DisposeAsync().ConfigureAwait(false);
                    connection.NetworkClient.Close();
                    AllConnections.Remove(connection);
                    return;
                }
                catch (Exception e)
                {
                    // e.g. a connection reset before the first byte; without this the exception escapes to
                    // SafeClientStartListen, which only logs it and never cleans up the connection
                    logger.LogInformation(HandshakeEventId, e,
                        "Connection failed before the protocol peek completed for {RemoteEndPoint}", connection.NetworkClient.Client.RemoteEndPoint);
                    await peaker.DisposeAsync().ConfigureAwait(false);
                    connection.NetworkClient.Close();
                    AllConnections.Remove(connection);
                    return;
                }
                var mark = peaker.FirstByte;
                if (mark != 0 && (mark < 32 || mark >= 127))
                {
                    var ssl = new SslStream(peaker, false);
                    connection.NetworkStream = ssl;
                    try
                    {
                        using var handshakeTimeout = DualSettings.HandshakeTimeout > TimeSpan.Zero
                            ? new CancellationTokenSource(DualSettings.HandshakeTimeout)
                            : null;
                        await ssl.AuthenticateAsServerAsync(
                            new SslServerAuthenticationOptions
                            {
                                ServerCertificate = DualSettings.Certificate,
                                ClientCertificateRequired = false,
                                EnabledSslProtocols = SslProtocols.None,
                                CertificateRevocationCheckMode = X509RevocationMode.Online,
                            },
                            handshakeTimeout?.Token ?? CancellationToken.None
                            ).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        // a failed handshake leaves ssl.IsAuthenticated false, so the
                        // existing cleanup branch below still runs
                        logger.LogInformation(HandshakeEventId, e,
                            "TLS handshake failed for {RemoteEndPoint}", connection.NetworkClient.Client.RemoteEndPoint);
                    }
                    if (!ssl.IsAuthenticated)
                    {
                        await ssl.DisposeAsync().ConfigureAwait(false);
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
                BaseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            }

            public Stream BaseStream { get; private set; }

            int firstByte = -1;
            bool firstByteRead;
            bool baseStreamAtEnd;

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

            /// <summary>
            /// Asynchronous, cancellable counterpart to <see cref="GetFirstByte" />; the byte is cached
            /// for <see cref="FirstByte" />/<see cref="HasFirstByte" />.
            /// </summary>
            public async ValueTask EnsureFirstByteAsync(CancellationToken cancellationToken)
            {
                if (firstByte != -1 || baseStreamAtEnd)
                    return;
                var b = new byte[1];
                var read = await BaseStream.ReadAsync(b.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    baseStreamAtEnd = true;
                    return;
                }
                firstByte = b[0];
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ArgumentNullException.ThrowIfNull(buffer);
                if (offset < 0 || offset + count > buffer.Length)
                    throw new ArgumentOutOfRangeException(nameof(offset));
                ArgumentOutOfRangeException.ThrowIfNegative(count);
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

            /// <summary>
            /// Async, cancellable counterpart to <see cref="Read(byte[], int, int)" />. Without it, a timeout
            /// could not abort the reads of the TLS handshake.
            /// </summary>
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (buffer.Length == 0)
                    return 0;
                if (firstByte == -1 && !baseStreamAtEnd)
                    await EnsureFirstByteAsync(cancellationToken).ConfigureAwait(false);
                if (!firstByteRead)
                {
                    firstByteRead = true;
                    if (baseStreamAtEnd)
                        return 0;
                    buffer.Span[0] = FirstByte;
                    if (buffer.Length == 1)
                        return 1;
                    var read = await BaseStream.ReadAsync(buffer[1..], cancellationToken).ConfigureAwait(false);
                    return read + 1;
                }
                return await BaseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(buffer);
                return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
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
