using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Services
{
    public class Http404Service : WebService
    {
        public Http404Service() 
            : base(ServerStage.CreateDocument)
        {
            Importance = WebProgressImportance.VeryLow;
        }

        public override bool CanWorkWith(WebProgressTask task)
            => true;

        public override Task ProgressTask(WebProgressTask task)
        {
            task.Document.ResponseHeader.StatusCode = HttpStateCode.NotFound;
            var sb = new StringBuilder();
            sb.Append("<html><head><title>404 NOT FOUND</title></head>");
            sb.Append("<body><h1>Error 404: Not Found</h1><p>The requested resource is not found.</p>");
            sb.AppendLine("<pre>");
            sb.AppendLine($"Protocol: {WebUtility.HtmlEncode(task.Document.RequestHeader.HttpProtocol)}");
            sb.AppendLine($"Method:   {WebUtility.HtmlEncode(task.Document.RequestHeader.ProtocolMethod)}");
            sb.AppendLine($"Url:      {WebUtility.HtmlEncode(task.Document.RequestHeader.Location.Url)}");
            sb.AppendLine($"Header:");
            foreach (var (key, value) in task.Document.RequestHeader.HeaderParameter)
                sb.AppendLine($"\t{WebUtility.HtmlEncode(key)}: {WebUtility.HtmlEncode(value)}");
            sb.AppendLine($"Body:");
            sb.AppendLine(WebUtility.HtmlEncode(task.Document.RequestHeader.Post.CompletePost));
            sb.Append($"</pre><p>Try to change the request to get your expected response.</p>");
            sb.Append($"<small>Created by <a href=\"https://github.com/Garados007/MaxLib.WebServer\" " +
                $"target=\"_blank\">MaxLib.WebServer</a>: {DateTime.UtcNow:r}</small></body></html>");
            task.Document.DataSources.Add(new HttpStringDataSource(sb.ToString())
            {
                MimeType = MimeType.TextHtml,
                TextEncoding = "utf-8",
            });
            return Task.CompletedTask;
        }
    }
}