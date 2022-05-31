using MaxLib.WebServer.Services;
using System;

#nullable enable

namespace MaxLib.WebServer.WebSocket.Echo
{
    class Program
    {
        static async Task Main()
        {
            WebServerLog.LogAdded += WebServerLog_LogAdded;
            using var server = new Server(new WebServerSettings(8000, 5000));
            // add services
            server.AddWebService(new HttpRequestParser());
            server.AddWebService(new HttpHeaderSpecialAction());
            server.AddWebService(new HttpResponseCreator());
            server.AddWebService(new HttpSender());
            // setup web socket
            using var websocket = new WebSocketService();
            websocket.Add(new EchoEndpoint());
            server.AddWebService(websocket);
            // run server until cancel received
            await server.RunAsync();
        }

        private static void WebServerLog_LogAdded(ServerLogItem item)
        {
            Console.WriteLine($"[{item.Date}] [{item.Type}] ({item.InfoType}) {item.SenderType}: {item.Information}");
        }
    }
}
