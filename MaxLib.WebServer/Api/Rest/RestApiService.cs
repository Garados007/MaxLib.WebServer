using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Api.Rest
{
    public class RestApiService : ApiService
    {
        public List<RestEndpoint> RestEndpoints { get; } = new List<RestEndpoint>();

        public RestApiService(params string[] endpoint)
            : base(endpoint)
        { }

        protected override async Task<HttpDataSource> HandleRequest(WebProgressTask task, string[] location)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            _ = location ?? throw new ArgumentNullException(nameof(location));
            var query = GetQueryArgs(task, location);
            foreach (var endpoint in RestEndpoints)
            {
                if (endpoint == null)
                    continue;
                var q = endpoint.Check(query);
                if (q == null)
                    continue;
                var result = await endpoint.GetSource(q.ParsedArguments)
                    .ConfigureAwait(false);
                if (result != null)
                    return result;
            }
            return NoEndpoint(task, query);
        }

        protected virtual RestQueryArgs GetQueryArgs(WebProgressTask task, string[] location)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            _ = location ?? throw new ArgumentNullException(nameof(location));
            return new RestQueryArgs(task.Request.Host, location, task.Request.Location.GetParameter, task.Request.Post, task.Session);
        }

        protected virtual HttpDataSource NoEndpoint(WebProgressTask task, RestQueryArgs args)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            _ = args ?? throw new ArgumentNullException(nameof(args));
            task.Response.StatusCode = HttpStateCode.NotFound;
            return new HttpStringDataSource("no endpoint");
        }
    }
}
