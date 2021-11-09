using System;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Services
{
    /// <summary>
    /// This service creates the response header and fill it with the necessary data. This will
    /// also discard any unread POST data to make the network stream reader for sending data.
    /// </summary>
    public class HttpResponseCreator : WebService
    {
        /// <summary>
        /// This service creates the response header and fill it with the necessary data. This will
        /// also discard any unread POST data to make the network stream reader for sending data.
        /// </summary>
        public HttpResponseCreator() : base(ServerStage.CreateResponse) { }

        public override async Task ProgressTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));

            var request = task.Request;
            var response = task.Response;
            response.FieldContentType = task.Document.PrimaryMime;
            response.SetActualDate();
            response.HttpProtocol = request.HttpProtocol;
            response.SetHeader(new (string, string?)[]
            {
                ("Connection", "keep-alive"),
                ("X-UA-Compatible", "IE=Edge"),
                ("Content-Length", task.Document.DataSources.Sum((s) => s.Length()).ToString()),
            });
            if (task.Document.PrimaryEncoding != null)
                response.HeaderParameter["Content-Type"] += "; charset=" +
                    task.Document.PrimaryEncoding;
            
            task.Request.Post.Dispose();

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public override bool CanWorkWith(WebProgressTask task)
        {
            return !task.Document.Information.ContainsKey($"block {nameof(HttpResponseCreator)}");
        }
    }
}
