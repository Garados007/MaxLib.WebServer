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
                ("Content-Length", task.Document.DataSources.Sum((s) => s.Length())?.ToString(CultureInfo.InvariantCulture) ?? ""),
            ]);
            if (task.Document.PrimaryEncoding != null)
                response.HeaderParameter["Content-Type"] += "; charset=" +
                    task.Document.PrimaryEncoding;

            return Task.CompletedTask;
        }

        public override bool CanWorkWith(WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            return !task.Document.Information.ContainsKey($"block {nameof(HttpResponseCreator)}");
        }
    }
}
