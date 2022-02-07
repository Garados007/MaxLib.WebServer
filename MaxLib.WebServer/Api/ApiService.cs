using System;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Api
{
    public abstract class ApiService : WebService
    {
        public ApiService(params string[] endpoint) 
            : base(ServerStage.CreateDocument)
        {
            this.endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        }

        private string[] endpoint;
        public string[] Endpoint
        {
            get => endpoint;
            set => endpoint = value ?? throw new ArgumentNullException(nameof(Endpoint));
        }

        public bool IgnoreCase { get; set; }

        protected abstract Task<HttpDataSource> HandleRequest(WebProgressTask task, string[] location);

        public override bool CanWorkWith(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            return task.Request.Location.StartsUrlWith(endpoint, IgnoreCase);
        }

        public override async Task ProgressTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            var tiles = task.Request.Location.DocumentPathTiles;
            var location = new string[tiles.Length - endpoint.Length];
            Array.Copy(tiles, endpoint.Length, location, 0, location.Length);
            var data = await HandleRequest(task, location).ConfigureAwait(false);
            if (data != null)
                task.Document.DataSources.Add(data);
            else task.Response.StatusCode = HttpStateCode.InternalServerError;
        }
    }
}
