using System.IO;

#nullable enable

namespace MaxLib.WebServer.WebSocket.Echo
{
    public class EchoEndpoint : WebSocketEndpoint<EchoConnection>
    {
        public override string? Protocol => null;

        protected override EchoConnection CreateConnection(Stream stream, HttpRequestHeader header)
        {
            return new EchoConnection(stream);
        }
    }
}
