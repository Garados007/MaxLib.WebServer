using System.Net;
using System;
using System.Threading.Tasks;

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

        public HttpRequestHeader Request { get; } = new HttpRequestHeader();

        public HttpResponseHeader Response { get; } = new HttpResponseHeader();

        public void Dispose()
        {
            Document?.Dispose();
        }

        internal Func<Task>? SwitchProtocolHandler { get; private set; } = null;

        /// <summary>
        /// A call to this method notify the web server that this connection will switch protocols 
        /// after all steps are finished. The web server will remove this connection from its 
        /// watch list and call <paramref name="handler"/> after its finished.
        /// <br />
        /// You as the caller are responsible to safely cleanup the connection it is no more
        /// used.
        /// </summary>
        /// <param name="handler">
        /// This handler will be called after the server has no more control of this connection.
        /// </param>
        public void SwitchProtocols(Func<Task> handler)
        {
            SwitchProtocolHandler = handler;
        }
    }
}
