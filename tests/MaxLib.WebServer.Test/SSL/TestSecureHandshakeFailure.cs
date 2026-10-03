using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MaxLib.WebServer.SSL;

namespace MaxLib.WebServer.Test.SSL
{
    [TestClass]
    public class TestSecureHandshakeFailure
    {
        private static X509Certificate2 CreateSelfSignedCertificate()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=maxlib-webserver-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1
            );
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(20).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestSecureWebServerRemovesTheConnectionWhenTheHandshakeThrows()
        {
            using var cert = CreateSelfSignedCertificate();
            var port = GetFreePort();
            var settings = new SecureWebServerSettings(port, connectionTimeout: 5000)
            {
                Certificate = cert,
            };
            var server = new SecureWebServer(settings);
            server.Start();
            try
            {
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
                    // garbage instead of a TLS ClientHello - makes AuthenticateAsServerAsync throw
                    var garbage = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                    await client.GetStream().WriteAsync(garbage).ConfigureAwait(false);
                }

                await WaitUntilAsync(() => server.AllConnections.Count == 0, TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);

                Assert.AreEqual(0, server.AllConnections.Count,
                    "the connection must be removed even though the TLS handshake threw");
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        public async Task TestSecureWebServerDropsAStalledConnectionThatNeverSendsAnything()
        {
            using var cert = CreateSelfSignedCertificate();
            var port = GetFreePort();
            var settings = new SecureWebServerSettings(port, connectionTimeout: 5000)
            {
                Certificate = cert,
                HandshakeTimeout = TimeSpan.FromMilliseconds(300),
            };
            var server = new SecureWebServer(settings);
            server.Start();
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
                // send nothing at all: only HandshakeTimeout ends this connection

                // confirm the connection was registered first, or the wait below could pass
                // before the server accepted it
                await WaitUntilAsync(() => server.AllConnections.Count == 1, TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
                Assert.AreEqual(1, server.AllConnections.Count,
                    "the connection was never registered - test setup is broken");

                await WaitUntilAsync(() => server.AllConnections.Count == 0, TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);

                Assert.AreEqual(0, server.AllConnections.Count,
                    "a stalled handshake that never receives any data must eventually be dropped");
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        public async Task TestDualSecureWebServerDropsAStalledConnectionThatNeverSendsAnything()
        {
            using var cert = CreateSelfSignedCertificate();
            var port = GetFreePort();
            var settings = new DualSecureWebServerSettings(port, 5000, cert)
            {
                HandshakeTimeout = TimeSpan.FromMilliseconds(300),
            };
            var server = new DualSecureWebServer(settings);
            server.Start();
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
                // send nothing at all: only HandshakeTimeout ends the initial protocol peek

                // confirm the connection was registered first, or the wait below could pass
                // before the server accepted it
                await WaitUntilAsync(() => server.AllConnections.Count == 1, TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
                Assert.AreEqual(1, server.AllConnections.Count,
                    "the connection was never registered - test setup is broken");

                await WaitUntilAsync(() => server.AllConnections.Count == 0, TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);

                Assert.AreEqual(0, server.AllConnections.Count,
                    "a stalled connection that never sends its first byte must eventually be dropped");
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        public async Task TestDualSecureWebServerRemovesTheConnectionWhenTheHandshakeThrows()
        {
            using var cert = CreateSelfSignedCertificate();
            var port = GetFreePort();
            var settings = new DualSecureWebServerSettings(port, 5000, cert);
            var server = new DualSecureWebServer(settings);
            server.Start();
            try
            {
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
                    // a non-ASCII first byte routes this down DualSecureWebServer's SSL branch;
                    // the rest is garbage instead of a real TLS ClientHello
                    var garbage = new byte[] { 0xFF, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                    await client.GetStream().WriteAsync(garbage).ConfigureAwait(false);
                }

                await WaitUntilAsync(() => server.AllConnections.Count == 0, TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);

                Assert.AreEqual(0, server.AllConnections.Count,
                    "the connection must be removed even though the TLS handshake threw");
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        public async Task TestDualSecureWebServerRemovesTheConnectionWhenResetBeforeTheFirstByte()
        {
            using var cert = CreateSelfSignedCertificate();
            var port = GetFreePort();
            var settings = new DualSecureWebServerSettings(port, 5000, cert);
            var server = new DualSecureWebServer(settings);
            server.Start();
            try
            {
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
                    // confirm the connection was registered first, so the abortive close races the server's read
                    await WaitUntilAsync(() => server.AllConnections.Count == 1, TimeSpan.FromSeconds(5))
                        .ConfigureAwait(false);
                    Assert.AreEqual(1, server.AllConnections.Count,
                        "the connection was never registered - test setup is broken");

                    // abortive close (RST) before sending any byte; Socket.Close(0) forces this instead of a graceful FIN
                    client.Client.Close(0);
                }

                await WaitUntilAsync(() => server.AllConnections.Count == 0, TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);

                Assert.AreEqual(0, server.AllConnections.Count,
                    "a connection reset before the first byte must not leak the connection");
            }
            finally
            {
                server.Stop();
            }
        }
    }
}
