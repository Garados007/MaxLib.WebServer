using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestServerAcceptLoopExceptionSafety
    {
        // Throws on the first accepted connection, then stops the loop on the second: the loop must
        // survive the exception and keep accepting. ServerMainTask() is called directly so an unhandled
        // exception fails the test instead of crashing the test process.
        private sealed class ThrowingAcceptServer : Server
        {
            public int ClientConnectedCalls;

            public ThrowingAcceptServer(WebServerSettings settings) : base(settings) { }

            public void SetListener(TcpListener listener) => Listener = listener;

            protected override void ClientConnected(TcpClient client)
            {
                Interlocked.Increment(ref ClientConnectedCalls);
                client.Dispose();
                if (ClientConnectedCalls == 1)
                    throw new InvalidOperationException("simulated accept-time failure");
                ServerExecution = false;
            }

            public void RunMainTaskOnceSynchronously()
            {
                ServerExecution = true;
                ServerMainTask();
            }
        }

        [TestMethod]
        public void TestServerMainTaskSurvivesAnExceptionFromClientConnected()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var client1 = new TcpClient();
            client1.Connect((IPEndPoint)listener.LocalEndpoint);
            using var client2 = new TcpClient();
            client2.Connect((IPEndPoint)listener.LocalEndpoint);
            // give the listener a moment to actually queue both pending connections
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (!listener.Pending() && DateTime.UtcNow < deadline)
                Thread.Sleep(5);

            var settings = new WebServerSettings(1, 5000) { ConnectionDelay = TimeSpan.FromMilliseconds(1) };
            var server = new ThrowingAcceptServer(settings);
            server.SetListener(listener);

            server.RunMainTaskOnceSynchronously();

            Assert.AreEqual(2, server.ClientConnectedCalls,
                "the accept loop must keep accepting further pending connections even after ClientConnected throws for an earlier one");
        }

        // Throws without ever touching (or disposing) the accepted client itself - simulating
        // a failure that happens before the framework's own connection bookkeeping (e.g.
        // reading RemoteEndPoint on a connection the peer already reset).
        private sealed class ThrowingAfterAcceptServer : Server
        {
            public ThrowingAfterAcceptServer(WebServerSettings settings) : base(settings) { }

            public void SetListener(TcpListener listener) => Listener = listener;

            protected override void ClientConnected(TcpClient client)
            {
                ServerExecution = false;
                throw new InvalidOperationException("simulated post-accept failure");
            }

            public void RunMainTaskOnceSynchronously()
            {
                ServerExecution = true;
                ServerMainTask();
            }
        }

        [TestMethod]
        public void TestServerMainTaskClosesTheAcceptedClientWhenClientConnectedThrows()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var client = new TcpClient();
            client.Connect((IPEndPoint)listener.LocalEndpoint);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (!listener.Pending() && DateTime.UtcNow < deadline)
                Thread.Sleep(5);

            var settings = new WebServerSettings(1, 5000) { ConnectionDelay = TimeSpan.FromMilliseconds(1) };
            var server = new ThrowingAfterAcceptServer(settings);
            server.SetListener(listener);

            server.RunMainTaskOnceSynchronously();

            // if the server never closes the accepted client, this blocks until the receive
            // timeout instead of observing a graceful EOF (read returning 0)
            client.ReceiveTimeout = 2000;
            var buffer = new byte[1];
            var read = client.GetStream().Read(buffer, 0, 1);
            Assert.AreEqual(0, read,
                "the server must close the accepted client if ClientConnected throws, or its socket leaks forever");
        }
    }
}
