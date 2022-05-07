using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.WebSocket.Echo
{
    public class EchoConnection : WebSocketConnection
    {
        public EchoConnection(Stream networkStream) 
            : base(networkStream)
        {
        }

        protected override async Task ReceiveClose(CloseReason? reason, string? info)
        {
            WebServerLog.Add(ServerLogType.Information, GetType(), "WebSocket", $"client close websocket ({reason}): {info}");
            if (!SendCloseSignal)
                await Close().ConfigureAwait(false);
        }

        protected override async Task ReceivedFrame(Frame frame)
        {
            await SendFrame(new Frame
            {
                OpCode = frame.OpCode,
                Payload = frame.Payload
            });
        }
    }
}
