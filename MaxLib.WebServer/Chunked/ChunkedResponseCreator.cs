﻿using MaxLib.WebServer.Lazy;
using System.Linq;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Chunked
{
    public class ChunkedResponseCreator : WebService
    {
        public bool OnlyWithLazy { get; private set; }

        public ChunkedResponseCreator(bool onlyWithLazy = false)
            : base(ServerStage.CreateResponse)
        {
            OnlyWithLazy = onlyWithLazy;
            if (onlyWithLazy) 
                Priority = WebServicePriority.High;
        }

        public override bool CanWorkWith(WebProgressTask task)
        {
            return !OnlyWithLazy || (task.Document.DataSources.Count > 0 &&
                task.Document.DataSources.Any((s) => s is LazySource ||
                    (s is Remote.MarshalSource ms && ms.IsLazy)
                ));
        }

        public override async Task ProgressTask(WebProgressTask task)
        {
            var request = task.Request;
            var response = task.Response;
            response.FieldContentType = task.Document.PrimaryMime;
            response.SetActualDate();
            response.HttpProtocol = request.HttpProtocol;
            response.SetHeader(new[]
            {
                ("Connection", "keep-alive"),
                ("X-UA-Compatible", "IE=Edge"),
                ("Transfer-Encoding", "chunked"),
            });
            if (task.Document.PrimaryEncoding != null)
                response.HeaderParameter["Content-Type"] += "; charset=" +
                    task.Document.PrimaryEncoding;
            task.Document.Information.Add("block default response creator", true);
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
