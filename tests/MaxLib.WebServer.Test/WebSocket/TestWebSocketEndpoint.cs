using MaxLib.WebServer.WebSocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Test.WebSocket
{
    [TestClass]
    public class TestWebSocketEndpoint
    {
        private sealed class DisposeTrackingConnection : WebSocketConnection
        {
            public bool Disposed { get; private set; }

            public DisposeTrackingConnection(Stream stream) : base(stream) { }

            public override async ValueTask DisposeAsync()
            {
                Disposed = true;
                await base.DisposeAsync().ConfigureAwait(false);
            }

            protected override Task ReceiveClose(CloseReason? reason, string? info) => Task.CompletedTask;

            protected override Task ReceivedFrame(Frame frame) => Task.CompletedTask;
        }

        private sealed class TestEndpoint : WebSocketEndpoint<DisposeTrackingConnection>
        {
            public override string? Protocol => null;

            public bool DisposeOnClose { get; set; } = true;

            protected override bool DisposeConnectionsOnClose => DisposeOnClose;

            protected override DisposeTrackingConnection? CreateConnection(Stream stream, HttpRequestHeader header)
                => new(stream);
        }

        [TestMethod]
        public async Task TestConnectionClosedDisposesTheConnectionByDefault()
        {
            // WebProgressTask.SwitchProtocols's own doc comment used to say the caller is
            // responsible for disposing a switched-protocol connection - true in general, but
            // WebSocketEndpoint<T> itself never did this either, meaning even the shipped
            // WebSocket echo example (which is built on it) leaked a socket per connection.
            var endpoint = new TestEndpoint();
            var connection = (DisposeTrackingConnection)(await endpoint.Create(new MemoryStream(), new HttpRequestHeader())
                .ConfigureAwait(false))!;

            await endpoint.HandleConnectionClosed(connection).ConfigureAwait(false);

            Assert.IsTrue(connection.Disposed);
        }

        [TestMethod]
        public async Task TestConnectionClosedLeavesTheConnectionAliveWhenOptedOut()
        {
            var endpoint = new TestEndpoint { DisposeOnClose = false };
            var connection = (DisposeTrackingConnection)(await endpoint.Create(new MemoryStream(), new HttpRequestHeader())
                .ConfigureAwait(false))!;

            await endpoint.HandleConnectionClosed(connection).ConfigureAwait(false);

            Assert.IsFalse(connection.Disposed);
        }
    }
}
