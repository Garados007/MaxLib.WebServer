#nullable enable

using System.IO;
using System.Threading.Tasks;

namespace MaxLib.WebServer.WebSocket
{
    public class WebSocketCloserEndpoint : WebSocketEndpoint<WebSocketCloserEndpoint.WebSocketCloseConnection>
    {
        public CloseReason CloseReason { get; }

        public string? Info { get; }

        public WebSocketCloserEndpoint(CloseReason closeReason, string? info)
        {
            CloseReason = closeReason;
            Info = info;
        }

        public override string? Protocol => null;

        protected sealed override WebSocketCloseConnection? CreateConnection(Stream stream, HttpRequestHeader header)
        {
            return new WebSocketCloseConnection(stream, this);
        }

        public class WebSocketCloseConnection : WebSocketConnection
        {
            public WebSocketCloseConnection(Stream networkStream, WebSocketCloserEndpoint endpoint) 
                : base(networkStream)
            {
                _ = Close(endpoint.CloseReason, endpoint.Info);
            }

            protected override Task ReceiveClose(CloseReason? reason, string? info)
            {
                return Task.CompletedTask;
            }

            protected override Task ReceivedFrame(Frame frame)
            {
                return Task.CompletedTask;
            }
        }
    }
}