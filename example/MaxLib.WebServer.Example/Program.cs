using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MaxLib.WebServer.Services;

namespace MaxLib.WebServer.Example
{
    class Program
    {
        static async Task Main(string[] args)
        {
            WebServerLog.SetLoggerFactory(LoggerFactory.Create(builder => builder.AddSimpleConsole()));
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
    }
}
