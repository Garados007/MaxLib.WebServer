using System;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Services
{
    /// <summary>
    /// WebServiceType.PreCreateDocument: Stellt ein festdefiniertes Dokument bereit. Dies ist unabhängig vom 
    /// angeforderten Pfad.
    /// </summary>
    public class StandardDocumentLoader : WebService
    {
        /// <summary>
        /// WebServiceType.PreCreateDocument: Stellt ein festdefiniertes Dokument bereit. Dies ist unabhängig vom 
        /// angeforderten Pfad.
        /// </summary>
        public StandardDocumentLoader()
            : base(ServerStage.CreateDocument)
        {
            Priority = WebServicePriority.VeryLow;
            Document = "<html><head><meta charset=\"utf-8\" /></head><body>Kein Dokument gefunden.</body></html>";
        }

        public string Document { get; set; }

        public override async Task ProgressTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));

            var source = new HttpStringDataSource(Document)
            {
                MimeType = MimeType.TextHtml
            };
            task.Response.StatusCode = HttpStateCode.OK;
            task.Document.DataSources.Add(source);
            task.Document.PrimaryEncoding = "utf-8";

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public override bool CanWorkWith(WebProgressTask task)
            => true;
    }
}
