using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestServerKeepAliveDispatch
    {
        // Runs the real ServerMainTask loop without accepting or processing any connection,
        // isolating the keep-alive re-dispatch decision.
        private sealed class RecordingServer : Server
        {
            public ConcurrentBag<HttpConnection> Dispatched { get; } = new();

            public RecordingServer(WebServerSettings settings) : base(settings) { }

            public void SetListener(TcpListener listener) => Listener = listener;

            protected override void ClientConnected(TcpClient client) { }

            protected override Task SafeClientStartListen(HttpConnection connection)
            {
                // deliberately never touches connection.LastWorkTime - simulates a dispatched
                // task that hasn't gotten around to actually running ClientStartListen yet
                Dispatched.Add(connection);
                return Task.CompletedTask;
            }

            public Thread StartMainTaskThread()
            {
                ServerExecution = true; // protected set - accessible directly from this subclass
                var thread = new Thread(ServerMainTask) { IsBackground = true };
                thread.Start();
                return thread;
            }
        }

        [TestMethod]
        public void TestAKeepAliveConnectionIsNotDispatchedTwiceBeforeItsTaskCanUpdateLastWorkTime()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var client = new TcpClient();
            client.Connect((IPEndPoint)listener.LocalEndpoint);
            using var accepted = listener.AcceptTcpClient();
            // give the accepted socket unread bytes, so `NetworkClient.Available > 0` holds
            // for every pass of the keep-alive scan below
            client.GetStream().Write(new byte[] { 1, 2, 3 });
            while (accepted.Available == 0)
                Thread.Sleep(5);

            // Settings.Port is irrelevant here - Listener is replaced with idleListener below
            // and Start() is never called
            var settings = new WebServerSettings(1, 5000) { ConnectionDelay = TimeSpan.FromMilliseconds(1) };
            var server = new RecordingServer(settings);
            using var idleListener = new TcpListener(IPAddress.Loopback, 0);
            idleListener.Start();
            server.SetListener(idleListener); // never gets a connection - Pending() stays false

            var connection = new HttpConnection
            {
                NetworkClient = accepted,
                LastWorkTime = Environment.TickCount,
            };
            server.KeepAliveConnections.Add(connection);
            server.AllConnections.Add(connection);

            var thread = server.StartMainTaskThread();
            try
            {
                Thread.Sleep(200); // several loop iterations at a 1ms delay
            }
            finally
            {
                server.Stop(); // ServerThread is null here, so this just flips ServerExecution
                thread.Join(TimeSpan.FromSeconds(2));
            }

            Assert.AreEqual(1, server.Dispatched.Count,
                "the same still-pending keep-alive connection must only be dispatched once");
        }
    }
}
