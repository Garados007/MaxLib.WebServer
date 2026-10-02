using MaxLib.WebServer.WebSocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Test.WebSocket
{
    [TestClass]
    public class TestWebSocketService
    {
        private sealed class MinimalConnection : WebSocketConnection
        {
            public MinimalConnection(Stream stream) : base(stream) { }
            protected override Task ReceiveClose(CloseReason? reason, string? info) => Task.CompletedTask;
            protected override Task ReceivedFrame(Frame frame) => Task.CompletedTask;
        }

        private sealed class ProtocolEndpoint(string? protocol) : WebSocketEndpoint<MinimalConnection>
        {
            public override string? Protocol => protocol;
            protected override MinimalConnection? CreateConnection(Stream stream, HttpRequestHeader header)
                => new(stream);
        }

        private static WebProgressTask CreateUpgradeTask()
        {
            var task = new WebProgressTask
            {
                NetworkStream = new MemoryStream(),
            };
            task.Request.SetHeader("Upgrade", "websocket");
            task.Request.SetHeader("Connection", "Upgrade");
            task.Request.SetHeader("Sec-WebSocket-Key", "dGhlIHNhbXBsZSBub25jZQ==");
            task.Request.SetHeader("Sec-WebSocket-Version", "13");
            return task;
        }

        [TestMethod]
        public async Task TestEndpointRequiringAProtocolIsNotSelectedWhenTheClientSendsNoProtocolHeader()
        {
            // a non-null Protocol requires the client to ask for it; no Sec-WebSocket-Protocol header must not match
            var service = new WebSocketService();
            service.Add(new ProtocolEndpoint("admin-v1"));
            var task = CreateUpgradeTask();

            await service.ProgressTask(task).ConfigureAwait(false);

            Assert.IsNull(task.SwitchProtocolHandler,
                "an endpoint that requires a protocol must not be selected by a client that asked for none");
        }

        [TestMethod]
        public async Task TestEndpointRequiringAProtocolIsSelectedWhenTheClientAsksForIt()
        {
            var service = new WebSocketService();
            service.Add(new ProtocolEndpoint("admin-v1"));
            var task = CreateUpgradeTask();
            task.Request.SetHeader("Sec-WebSocket-Protocol", "admin-v1");

            await service.ProgressTask(task).ConfigureAwait(false);

            Assert.IsNotNull(task.SwitchProtocolHandler);
        }

        [TestMethod]
        public async Task TestEndpointWithNoProtocolIsSelectedWhenTheClientSendsNoProtocolHeader()
        {
            // a null Protocol still matches a client that sends no Sec-WebSocket-Protocol header
            var service = new WebSocketService();
            service.Add(new ProtocolEndpoint(null));
            var task = CreateUpgradeTask();

            await service.ProgressTask(task).ConfigureAwait(false);

            Assert.IsNotNull(task.SwitchProtocolHandler);
        }

        [TestMethod]
        public async Task TestEndpointWithNoProtocolIsNotSelectedWhenTheClientAsksForAProtocol()
        {
            var service = new WebSocketService();
            service.Add(new ProtocolEndpoint(null));
            var task = CreateUpgradeTask();
            task.Request.SetHeader("Sec-WebSocket-Protocol", "some-protocol");

            await service.ProgressTask(task).ConfigureAwait(false);

            Assert.IsNull(task.SwitchProtocolHandler,
                "an endpoint with no protocol must not be selected by a client that asked for one");
        }

        [TestMethod]
        public async Task TestEndpointWithAMixedCaseProtocolIsSelectedWhenTheClientAsksForItVerbatim()
        {
            // the requested protocol list is lower-cased, so a mixed-case endpoint Protocol must still match
            var service = new WebSocketService();
            service.Add(new ProtocolEndpoint("Chat"));
            var task = CreateUpgradeTask();
            task.Request.SetHeader("Sec-WebSocket-Protocol", "Chat");

            await service.ProgressTask(task).ConfigureAwait(false);

            Assert.IsNotNull(task.SwitchProtocolHandler,
                "an endpoint whose Protocol contains an uppercase character must still be selected");
        }

        [TestMethod]
        public async Task TestResponseEchoesTheClientsOwnCasingNotTheEndpointsRegisteredCasing()
        {
            // the response must echo the client's offered token verbatim, not the endpoint's casing
            var service = new WebSocketService();
            service.Add(new ProtocolEndpoint("chat"));
            var task = CreateUpgradeTask();
            task.Request.SetHeader("Sec-WebSocket-Protocol", "Chat");

            await service.ProgressTask(task).ConfigureAwait(false);

            Assert.AreEqual("Chat", task.Response.GetHeader("Sec-WebSocket-Protocol"),
                "the response must echo the client's own casing, not the endpoint's");
        }
    }
}
