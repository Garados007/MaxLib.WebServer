using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Services
{
    /// <summary>
    /// This service creates the response header and fill it with the necessary data.
    /// </summary>
    /// <remarks>
    /// Any unread POST data is discarded later, once the response has actually been sent — see
    /// <see cref="HttpSender" />. Doing it here instead, before the response is sent, would risk
    /// the client never finding out about a response (e.g. a timeout status) if discarding that
    /// data stalls or fails and the underlying connection has to be closed as a result.
    /// </remarks>
    public class HttpResponseCreator : WebService
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<HttpResponseCreator>();
        static readonly EventId NoChunkedSenderEventId = new(0, "NoChunkedSender");

        /// <summary>
        /// This service creates the response header and fill it with the necessary data.
        /// </summary>
        public HttpResponseCreator() : base(ServerStage.CreateResponse) { }

        public override Task ProgressTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));

            var request = task.Request;
            var response = task.Response;
            response.FieldContentType = task.Document.PrimaryMime;
            response.SetActualDate();
            response.HttpProtocol = request.HttpProtocol;
            response.SetHeader(
            [
                ("Connection", "keep-alive"),
                ("X-UA-Compatible", "IE=Edge"),
            ]);
            if (task.Document.PrimaryEncoding != null)
                response.HeaderParameter["Content-Type"] += "; charset=" +
                    task.Document.PrimaryEncoding;

            if (task.Document.DataSources.Any(s => s.Length() is null))
            {
                // A null Length() means "unknown/streaming size" - Sum() over this would
                // silently treat it as 0, undercounting Content-Length while the full body is
                // still written to the wire (desyncing any client/proxy relying on that header
                // for response framing). Only a genuinely chunked send (Chunked.ChunkedSender)
                // expresses this correctly on the wire; if none is registered to do that, fail
                // loudly instead of ever emitting a wrong Content-Length.
                if (task.Server?.GetWebServices<Chunked.ChunkedSender>().Any() != true)
                {
                    logger.LogError(NoChunkedSenderEventId,
                        "Rejecting a response containing a data source with an unknown length: " +
                        "no {Sender} is registered to send it correctly.",
                        nameof(Chunked.ChunkedSender));
                    response.StatusCode = HttpStateCode.InternalServerError;
                    foreach (var source in task.Document.DataSources)
                        source.Dispose();
                    task.Document.DataSources.Clear();
                    response.HeaderParameter["Content-Length"] = "0";
                }
                // else: a chunked sender is registered - leave Content-Length unset here and
                // let it (and its paired Chunked.ChunkedResponseCreator, if also registered)
                // take over framing for this response instead.
                return Task.CompletedTask;
            }

            response.HeaderParameter["Content-Length"] =
                task.Document.DataSources.Sum((s) => s.Length())?.ToString(CultureInfo.InvariantCulture) ?? "";
            return Task.CompletedTask;
        }

        public override bool CanWorkWith(WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            return !task.Document.Information.ContainsKey($"block {nameof(HttpResponseCreator)}");
        }
    }
}
