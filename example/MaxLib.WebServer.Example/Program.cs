using System;
using MaxLib.WebServer.Services;

namespace MaxLib.WebServer.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            WebServerLog.LogAdded += WebServerLog_LogAdded;
            var server = new Server(new WebServerSettings(8000, 5000));
            // add services
            server.AddWebService(new HttpRequestParser());
            server.AddWebService(new HttpHeaderSpecialAction());
            server.AddWebService(new StandardDocumentLoader() { Document = "Hello World!" });
            server.AddWebService(new HttpResponseCreator());
            server.AddWebService(new HttpSender());
            // start server
            server.Start();
        }

        private static void WebServerLog_LogAdded(ServerLogItem item)
        {
            Console.WriteLine($"[{item.Date}] [{item.Type}] ({item.InfoType}) {item.SenderType}: {item.Information}");
        }
    }
}
