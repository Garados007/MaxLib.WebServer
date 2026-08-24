using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MaxLib.WebServer.Builder.Debugger.Example
{
    class Program
    {
        static async Task Main()
        {
            WebServerLog.SetLoggerFactory(LoggerFactory.Create(builder => builder.AddSimpleConsole()));
            using var server = new Server(new WebServerSettings(8000, 5000));
            server.InitialDefault();
            server.AddWebService(new IndexService());

            // Scan this whole assembly for Service subclasses at once - GreetingService,
            // MathService, ... alongside every deliberately broken one in BrokenEndpoints.cs.
            // The report below shows every scanned type/method, whether it was built, and why
            // not for the ones that weren't.
            var built = Service.Build(Assembly.GetExecutingAssembly(), out var report);
            if (built != null)
                server.AddWebService(built);

            Console.WriteLine(
                $"Build report: {report.CountByStatus(BuildReportStatus.Accepted)} accepted, " +
                $"{report.CountByStatus(BuildReportStatus.Ignored)} ignored, " +
                $"{report.CountByStatus(BuildReportStatus.Failed)} failed."
            );

            server.AddWebService(new DebuggerService(server, report));

            Console.WriteLine("Listening on http://localhost:8000/ - open it in a browser.");
            Console.WriteLine("Debugger: http://localhost:8000/_debugger/");

            // run server until cancel received
            await server.RunAsync();
        }
    }
}
