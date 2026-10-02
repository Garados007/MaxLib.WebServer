using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MaxLib.WebServer.SSL;

namespace MaxLib.WebServer.Test.SSL
{
    [TestClass]
    public class TestSecureWebServerAcceptLoopExceptionSafety
    {
        private static X509Certificate2 CreateSelfSignedCertificate()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=maxlib-webserver-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1
            );
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));
        }

        // Mirrors TestServerAcceptLoopExceptionSafety's ThrowingAcceptServer for SecureWebServer.SecureMainTask's accept loop.
        private sealed class ThrowingSecureAcceptServer : SecureWebServer
        {
            public int SecureClientConnectedCalls;

            public ThrowingSecureAcceptServer(SecureWebServerSettings settings) : base(settings) { }

            public void SetSecureListener(TcpListener listener) => SecureListener = listener;

            protected override void SecureClientConnected(TcpClient client)
            {
                Interlocked.Increment(ref SecureClientConnectedCalls);
                client.Dispose();
                if (SecureClientConnectedCalls == 1)
                    throw new InvalidOperationException("simulated accept-time failure");
                ServerExecution = false;
            }

            public void RunSecureMainTaskOnceSynchronously()
            {
                ServerExecution = true;
                SecureMainTask();
            }
        }

        [TestMethod]
        public void TestSecureMainTaskSurvivesAnExceptionFromSecureClientConnected()
        {
            using var cert = CreateSelfSignedCertificate();
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var client1 = new TcpClient();
            client1.Connect((IPEndPoint)listener.LocalEndpoint);
            using var client2 = new TcpClient();
            client2.Connect((IPEndPoint)listener.LocalEndpoint);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (!listener.Pending() && DateTime.UtcNow < deadline)
                Thread.Sleep(5);

            var settings = new SecureWebServerSettings(1, 1, 5000) { Certificate = cert };
            var server = new ThrowingSecureAcceptServer(settings);
            server.SetSecureListener(listener);

            server.RunSecureMainTaskOnceSynchronously();

            Assert.AreEqual(2, server.SecureClientConnectedCalls,
                "the accept loop must keep accepting further pending connections even after SecureClientConnected throws for an earlier one");
        }

        // Throws without ever touching (or disposing) the accepted client itself.
        private sealed class ThrowingAfterSecureAcceptServer : SecureWebServer
        {
            public ThrowingAfterSecureAcceptServer(SecureWebServerSettings settings) : base(settings) { }

            public void SetSecureListener(TcpListener listener) => SecureListener = listener;

            protected override void SecureClientConnected(TcpClient client)
            {
                ServerExecution = false;
                throw new InvalidOperationException("simulated post-accept failure");
            }

            public void RunSecureMainTaskOnceSynchronously()
            {
                ServerExecution = true;
                SecureMainTask();
            }
        }

        [TestMethod]
        public void TestSecureMainTaskClosesTheAcceptedClientWhenSecureClientConnectedThrows()
        {
            using var cert = CreateSelfSignedCertificate();
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var client = new TcpClient();
            client.Connect((IPEndPoint)listener.LocalEndpoint);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (!listener.Pending() && DateTime.UtcNow < deadline)
                Thread.Sleep(5);

            var settings = new SecureWebServerSettings(1, 1, 5000) { Certificate = cert };
            var server = new ThrowingAfterSecureAcceptServer(settings);
            server.SetSecureListener(listener);

            server.RunSecureMainTaskOnceSynchronously();

            // if the server never closes the accepted client, this blocks until the receive
            // timeout instead of observing a graceful EOF (read returning 0)
            client.ReceiveTimeout = 2000;
            var buffer = new byte[1];
            var read = client.GetStream().Read(buffer, 0, 1);
            Assert.AreEqual(0, read,
                "the server must close the accepted client if SecureClientConnected throws, or its socket leaks forever");
        }
    }
}
