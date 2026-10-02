using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MaxLib.WebServer.SSL;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestSecureWebServerKeepAliveDispatch
    {
        // Runs the real SecureMainTask loop of an HTTPS-only server (Server.ServerMainTask never starts)
        // without accepting any connection, to check that it polls/evicts KeepAliveConnections itself.
        private sealed class RecordingSecureServer : SecureWebServer
        {
            public bool StartListenCalled;

            public RecordingSecureServer(SecureWebServerSettings settings) : base(settings) { }

            public void SetSecureListener(TcpListener listener) => SecureListener = listener;

            protected override void SecureClientConnected(TcpClient client) { }

            protected override Task SafeClientStartListen(HttpConnection connection)
            {
                StartListenCalled = true;
                return Task.CompletedTask;
            }

            public Thread StartSecureMainTaskThread()
            {
                ServerExecution = true; // protected set - accessible directly from this subclass
                var thread = new Thread(SecureMainTask) { IsBackground = true };
                thread.Start();
                return thread;
            }
        }

        [TestMethod]
        public void TestSecureMainTaskEvictsAStaleKeepAliveConnectionWhenUnsafePortIsDisabled()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var client = new TcpClient();
            client.Connect((IPEndPoint)listener.LocalEndpoint);
            using var accepted = listener.AcceptTcpClient();

            var settings = new SecureWebServerSettings(0, 1) // securePort, connectionTimeout=1ms
            {
                ConnectionDelay = TimeSpan.FromMilliseconds(1),
            };
            Assert.IsFalse(settings.EnableUnsafePort,
                "the two-argument constructor must select the documented HTTPS-only mode");

            var server = new RecordingSecureServer(settings);
            using var idleListener = new TcpListener(IPAddress.Loopback, 0);
            idleListener.Start();
            server.SetSecureListener(idleListener); // never gets a connection - Pending() stays false

            var connection = new HttpConnection
            {
                NetworkClient = accepted,
                LastWorkTime = Environment.TickCount - 10_000, // already far past the 1ms timeout
            };
            server.KeepAliveConnections.Add(connection);
            server.AllConnections.Add(connection);

            var thread = server.StartSecureMainTaskThread();
            try
            {
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
                while (server.KeepAliveConnections.Count > 0 && DateTime.UtcNow < deadline)
                    Thread.Sleep(5);
            }
            finally
            {
                server.Stop();
                thread.Join(TimeSpan.FromSeconds(2));
            }

            Assert.AreEqual(0, server.KeepAliveConnections.Count,
                "SecureMainTask must evict a timed-out keep-alive connection even when EnableUnsafePort is false, or it would never be processed by anything");
            Assert.IsFalse(server.StartListenCalled,
                "an already-timed-out connection must be evicted, not re-dispatched");
        }

        [TestMethod]
        public void TestKeepAliveGuardTracksStartTimeDecisionNotALaterEnableUnsafePortToggle()
        {
            // securePort 0 lets the OS assign an unused ephemeral port
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var client = new TcpClient();
            client.Connect((IPEndPoint)listener.LocalEndpoint);
            using var accepted = listener.AcceptTcpClient();

            var settings = new SecureWebServerSettings(0, 1) // securePort, connectionTimeout=1ms
            {
                ConnectionDelay = TimeSpan.FromMilliseconds(1),
            };
            Assert.IsFalse(settings.EnableUnsafePort,
                "the two-argument constructor must select the documented HTTPS-only mode");

            var server = new RecordingSecureServer(settings);
            server.Start(); // baseLoopStarted is captured here as false - Server.ServerMainTask never starts
            try
            {
                // toggling the setting after Start() must not change the decision of whether the base loop runs,
                // or the keep-alive guard would stop servicing KeepAliveConnections
                settings.EnableUnsafePort = true;

                var connection = new HttpConnection
                {
                    NetworkClient = accepted,
                    LastWorkTime = Environment.TickCount - 10_000, // already far past the 1ms timeout
                };
                server.KeepAliveConnections.Add(connection);
                server.AllConnections.Add(connection);

                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
                while (server.KeepAliveConnections.Count > 0 && DateTime.UtcNow < deadline)
                    Thread.Sleep(5);

                Assert.AreEqual(0, server.KeepAliveConnections.Count,
                    "SecureMainTask must keep evicting stale keep-alive connections after EnableUnsafePort " +
                    "is toggled at runtime, since Server.ServerMainTask was never actually started to pick up the slack");
            }
            finally
            {
                server.Stop();
            }
        }
    }
}
