using MaxLib.WebServer.Services;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestServer
    {
        // Throws a plain, non-HttpException exception - the kind WebServiceGroup.Execute()
        // doesn't catch on its own - from whichever stage it is registered at.
        private sealed class ThrowingService : WebService
        {
            public ThrowingService(ServerStage stage) : base(stage) { }
            public override bool CanWorkWith(WebProgressTask task) => true;
            public override Task ProgressTask(WebProgressTask task)
                => throw new InvalidOperationException("boom");
        }

        [TestMethod]
        public async Task TestUnhandledExceptionSendsFallbackResponseAndRemovesTheConnection()
        {
            var server = new TestWebServer();
            server.AddWebService(new ThrowingService(ServerStage.ProcessDocument));
            server.AddWebService(new HttpResponseCreator());
            server.AddWebService(new HttpSender());

            var test = server.CreateTest();
            test.CurrentStage = ServerStage.FIRST_STAGE;
            var connection = new HttpConnection();
            test.SetConnection(connection);
            server.AllConnections.Add(connection);

            using var response = new MemoryStream();
            test.Task.NetworkStream = response;

            await server.RunProcessTask(test.Task, connection).ConfigureAwait(false);

            response.Position = 0;
            using var r = new StreamReader(response);
            var text = r.ReadToEnd();
            StringAssert.Contains(text, "500 Internal Server Error");

            Assert.AreEqual(HttpConnectionType.Close, test.Request.FieldConnection);
            Assert.IsFalse(server.AllConnections.Contains(connection),
                "the connection must be removed even though the request handler threw");
            Assert.IsFalse(server.KeepAliveConnections.Contains(connection));
        }

        [TestMethod]
        public async Task TestUnhandledExceptionDuringSendResponseDoesNotAttemptASecondResponse()
        {
            // Simulates the exception happening while the response is already being
            // transmitted: bytes may already be on the wire, so no fallback response may be
            // attempted - it would either corrupt what was already sent or, at best, send a
            // second bogus response the client never asked for.
            var server = new TestWebServer();
            server.AddWebService(new ThrowingService(ServerStage.SendResponse));

            var test = server.CreateTest();
            test.CurrentStage = ServerStage.SendResponse;
            var connection = new HttpConnection();
            test.SetConnection(connection);
            server.AllConnections.Add(connection);

            using var response = new MemoryStream();
            test.Task.NetworkStream = response;

            await server.RunProcessTask(test.Task, connection).ConfigureAwait(false);

            Assert.AreEqual(0, response.Length,
                "no fallback response may be written once SendResponse has already started");
            Assert.AreEqual(HttpConnectionType.Close, test.Request.FieldConnection);
            Assert.IsFalse(server.AllConnections.Contains(connection));
        }

        [TestMethod]
        public void TestTryAdmitConnectionAllowsConnectionsWhenNoLimitIsConfigured()
        {
            var server = new TestWebServer();
            using var client = new TcpClient();

            Assert.IsTrue(server.RunTryAdmitConnection(client));
        }

        [TestMethod]
        public void TestTryAdmitConnectionRejectsOnceTheConfiguredLimitIsReached()
        {
            var server = new TestWebServer
            {
                Settings = { MaxConcurrentConnections = 1 },
            };
            server.AllConnections.Add(new HttpConnection());
            using var client = new TcpClient();

            Assert.IsFalse(server.RunTryAdmitConnection(client));
        }

        [TestMethod]
        public void TestTryAdmitConnectionAllowsConnectionsBelowTheConfiguredLimit()
        {
            var server = new TestWebServer
            {
                Settings = { MaxConcurrentConnections = 2 },
            };
            server.AllConnections.Add(new HttpConnection());
            using var client = new TcpClient();

            Assert.IsTrue(server.RunTryAdmitConnection(client));
        }
    }
}
