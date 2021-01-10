using System;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Services
{
    [Obsolete("This service has no usage. Just remove any reference from the code.")]
    public class HttpHeaderPostParser : WebService
    {
        public HttpHeaderPostParser()
            : base(ServerStage.ParseRequest)
        {
            Priority = WebServicePriority.VeryHigh;
        }

        public override async Task ProgressTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));

            var header = task.Request;
            // //Accept
            // if (header.HeaderParameter.TryGetValue("Accept", out string value))
            // {
            //     header.FieldAccept.AddRange(value.Split(
            //         new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries));
            // }
            // //Accept-Encoding
            // if (header.HeaderParameter.TryGetValue("Accept-Encoding", out value))
            // {
            //     header.FieldAcceptEncoding.AddRange(value.Split(
            //         new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
            // }
            // //Connection
            // if (header.HeaderParameter.TryGetValue("Connection", out value))
            // {
            //     if (value.ToLower() == "keep-alive") 
            //         header.FieldConnection = HttpConnectionType.KeepAlive;
            // }
            // //Host
            // if (header.HeaderParameter.TryGetValue("Host", out value))
            // {
            //     header.Host = value;
            // }
            // //Cookie
            // if (header.HeaderParameter.TryGetValue("Cookie", out value))
            // {
            //     header.Cookie.SetRequestCookieString(value);
            // }

            await Task.CompletedTask;
        }

        public override bool CanWorkWith(WebProgressTask task)
            => true;
    }
}
