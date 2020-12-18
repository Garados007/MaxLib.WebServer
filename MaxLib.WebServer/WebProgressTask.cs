using System;

namespace MaxLib.WebServer
{
    public class WebProgressTask : IDisposable
    {
        public HttpDocument Document { get; set; }

        public System.IO.Stream NetworkStream { get; set; }

        public WebServiceType NextTask { get; set; }

        public WebServiceType CurrentTask { get; set; }

        public Server Server { get; set; }

        public HttpConnection Connection { get; set; }

        public void Dispose()
        {
            Document?.Dispose();
        }
    }
}
