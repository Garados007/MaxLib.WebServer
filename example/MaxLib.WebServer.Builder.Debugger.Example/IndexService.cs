using System.Threading.Tasks;

namespace MaxLib.WebServer.Builder.Debugger.Example
{
    /// <summary>
    /// A small, hand-written landing page at "/" that links to every demo endpoint in this
    /// example, plus the debugger itself, so you don't have to remember any of the URLs while
    /// exploring. Some links intentionally 404 - each of those has a matching "explain in
    /// debugger" link that deep-links straight into the request-routing simulator for that exact
    /// path.
    /// </summary>
    public class IndexService : WebService
    {
        public IndexService() : base(ServerStage.CreateDocument)
        {
        }

        public override bool CanWorkWith(WebProgressTask task)
            => task.Request.Location.DocumentPath == "/";

        public override Task ProgressTask(WebProgressTask task)
        {
            task.Document.DataSources.Add(new HttpStringDataSource(Properties.Resources.Debugger_IndexShell)
            {
                MimeType = MimeType.TextHtml,
                TextEncoding = "utf-8",
            });
            return Task.CompletedTask;
        }
    }
}
