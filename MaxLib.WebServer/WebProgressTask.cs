using System.Net;
using System;

#nullable enable

namespace MaxLib.WebServer
{
    public class WebProgressTask : IDisposable
    {
        public HttpDocument Document { get; } = new HttpDocument();

        public System.IO.Stream? NetworkStream { get; set; }

        /// <summary>
        /// Normaly this property points to the next stage of the <see cref="CurrentStage" />. 
        /// This property can be changed to skip or repeat some stages.
        /// </summary>
        public ServerStage NextStage { get; set; }

        /// <summary>
        /// The current stage this task is currently in.
        /// </summary>
        public ServerStage CurrentStage { get; set; }

        public Server? Server { get; set; }

        public HttpConnection? Connection { get; set; }

        public Sessions.Session? Session { get; set; }

#pragma warning disable CS0618
        public HttpRequestHeader Request => Document.RequestHeader;

        public HttpResponseHeader Response => Document.ResponseHeader;
#pragma warning restore CS0618

        public void Dispose()
        {
            Document?.Dispose();
        }
    }
}
