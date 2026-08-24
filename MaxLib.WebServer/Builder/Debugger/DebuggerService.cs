using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// An opt-in, hand-written diagnostics endpoint for the <see cref="Tools.Generator" />
    /// builder pipeline. Register it exactly like any other
    /// <see cref="WebService" /> - <c>server.AddWebService(new DebuggerService(server, report))</c>
    /// - to expose:
    /// <list type="bullet">
    /// <item>a self-contained HTML page at <see cref="BasePath" /> with a foldable/searchable
    /// view of the <see cref="BuildReport" /> and a request-routing simulator,</item>
    /// <item><c>{BasePath}/api/build-report</c>: the <see cref="BuildReport" /> as JSON,</item>
    /// <item><c>{BasePath}/api/route</c>: a JSON array of one <see cref="RoutingDryRun" /> result
    /// per <see cref="ServerStage" /> (or just the one requested), against the live
    /// <see cref="Server" />, driven by <c>method</c>/<c>path</c>/<c>stage</c> query parameters
    /// and <c>h.&lt;name&gt;=&lt;value&gt;</c> for simulated headers. Omit <c>stage</c> to
    /// simulate every stage at once.</item>
    /// </list>
    /// Nothing about registering this service changes the behavior of
    /// <see cref="Service.Build()" /> or any of its overloads - it is entirely opt-in and has no
    /// cost when not registered.
    /// </summary>
    public sealed class DebuggerService : WebService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        /// <summary>
        /// The build report to display. Can be replaced at any time (e.g. after re-running
        /// <see cref="Service.Build()" />).
        /// </summary>
        public BuildReport? BuildReport { get; set; }

        /// <summary>
        /// The server whose live <see cref="MaxLib.WebServer.Server.WebServiceGroups" /> the
        /// routing simulator dry-runs against.
        /// </summary>
        public Server Server { get; }

        /// <summary>
        /// The URL path prefix this service answers to. Defaults to <c>/_debugger</c>.
        /// </summary>
        public string BasePath { get; }

        public DebuggerService(Server server, BuildReport? buildReport = null, string basePath = "/_debugger")
            : base(ServerStage.CreateDocument)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(basePath);
            Server = server;
            BuildReport = buildReport;
            BasePath = basePath;
            // must win before user services register their own catch-alls for this sub-tree.
            Priority = WebServicePriority.High;
        }

        public override bool CanWorkWith(WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            var path = task.Request.Location.DocumentPath;
            return string.Equals(path, BasePath, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(BasePath + "/", StringComparison.OrdinalIgnoreCase);
        }

        public override Task ProgressTask(WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            var path = task.Request.Location.DocumentPath;
            var rest = path.Length <= BasePath.Length ? "" : path[BasePath.Length..];
            return rest switch
            {
                "" or "/" => ServeHtmlShell(task),
                "/api/build-report" => ServeBuildReportJson(task),
                "/api/route" => ServeRouteJson(task),
                _ => ServeNotFound(task),
            };
        }

        private Task ServeHtmlShell(WebProgressTask task)
        {
            var html = $"<script>window.__debuggerBasePath = {JsonSerializer.Serialize(BasePath)};</script>\n"
                + Properties.Resources.Debugger_Shell;
            task.Document.DataSources.Add(new HttpStringDataSource(html)
            {
                MimeType = MimeType.TextHtml,
                TextEncoding = "utf-8",
            });
            return Task.CompletedTask;
        }

        private Task ServeBuildReportJson(WebProgressTask task)
            => WriteJson(task, BuildReport ?? new BuildReport());

        /// <summary>
        /// Every real (non-alias) <see cref="ServerStage" />, in dispatch order.
        /// </summary>
        private static readonly ServerStage[] AllStages =
        [
            ServerStage.ReadRequest,
            ServerStage.ParseRequest,
            ServerStage.CreateDocument,
            ServerStage.ProcessDocument,
            ServerStage.CreateResponse,
            ServerStage.SendResponse,
            ServerStage.Cleanup,
        ];

        private Task ServeRouteJson(WebProgressTask task)
        {
            var query = task.Request.Location.GetParameter;
            var method = query.TryGetValue("method", out var m) ? m : "GET";
            var path = query.TryGetValue("path", out var p) ? p : "/";
            var stageParam = query.TryGetValue("stage", out var s) ? s : "";
            var headers = query
                .Where(kv => kv.Key.StartsWith("h.", StringComparison.Ordinal))
                .ToDictionary(kv => kv.Key[2..], kv => kv.Value);

            var stages = !string.IsNullOrEmpty(stageParam) && Enum.TryParse<ServerStage>(stageParam, true, out var parsed)
                ? [parsed]
                : AllStages;

            var reports = stages
                .Select(stage => RoutingDryRun.Run(Server.WebServiceGroups[stage], method, path, headers))
                .ToList();
            return WriteJson(task, reports);
        }

        private static Task ServeNotFound(WebProgressTask task)
        {
            task.Response.StatusCode = HttpStateCode.NotFound;
            task.Document.DataSources.Add(new HttpStringDataSource("Not Found")
            {
                MimeType = MimeType.TextPlain,
                TextEncoding = "utf-8",
            });
            return Task.CompletedTask;
        }

        private static Task WriteJson<T>(WebProgressTask task, T value)
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            task.Document.DataSources.Add(new HttpStringDataSource(json)
            {
                MimeType = MimeType.ApplicationJson,
                TextEncoding = "utf-8",
            });
            return Task.CompletedTask;
        }
    }
}
