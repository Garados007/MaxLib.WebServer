using System;
using System.Threading.Tasks;
using MaxLib.WebServer.Services;

namespace MaxLib.WebServer.Example
{
    class Program
    {
        static async Task Main(string[] args)
        {
            WebServerLog.LogAdded += WebServerLog_LogAdded;
            using var server = new Server(new WebServerSettings(8000, 5000));
            // add services
            server.AddWebService(new HttpRequestParser());
            server.AddWebService(new HttpHeaderSpecialAction());
            server.AddWebService(new Http404Service());
            server.AddWebService(new HttpResponseCreator());
            server.AddWebService(new HttpSender());
            // run server until cancel received
            await server.RunAsync();
        }

        private static void WebServerLog_LogAdded(ServerLogItem item)
        {
            Console.WriteLine($"[{item.Date}] [{item.Type}] ({item.InfoType}) {item.SenderType}: {item.Information}");
        }
    }
}
