using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Testing
{
    public class TestTask : WebServerTaskCreator
    {
        public TestWebServer WebServer { get; }

        public TestTask(TestWebServer webServer)
            : base()
        {
            WebServer = webServer ?? throw new ArgumentNullException(nameof(webServer));
            Task.Server = webServer;
        }

        public Task Start()
        {
            return Start(WebServer);
        }

        public MemoryStream SetStream(string input)
            => SetStream(input, Encoding.UTF8);

        public MemoryStream SetStream(string input, Encoding encoding)
        {
            _ = input ?? throw new ArgumentNullException(nameof(input));
            _ = encoding ?? throw new ArgumentNullException(nameof(encoding));
            var output = new MemoryStream();
            var inputStream = new MemoryStream(encoding.GetBytes(input));
            SetStream(inputStream, output);
            return output;
        }

        /// <summary>
        /// Generate a random session and assign it to the task
        /// </summary>
        public void SetConnection()
            => SetConnection(WebServer.CreateRandomConnection());

        public void SetConnection(HttpConnection connection)
        {
            Task.Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>
        /// Set an empty input stream and returns the output stream. It is usefull
        /// if you won't test the input parsing at all.
        /// </summary>
        /// <returns>the output stream</returns>
        public MemoryStream SetStream()
            => SetStream("");

        public void SetInfoObject(object key, object value)
            => Task.Document.Information[key] = value;

        public ServerStage CurrentStage
        {
            get => Task.CurrentStage;
            set
            {
                Task.CurrentStage = value;
                if (value == ServerStage.FINAL_STAGE)
                    Task.NextStage = value;
                else Task.NextStage = (ServerStage)(1 + (int)value);
            }
        }

        public List<HttpDataSource> GetDataSources()
            => Task.Document.DataSources;

        public object GetInfoObject(object key)
            => Task.Document.Information.TryGetValue(key, out object value) ? value : default;

        public HttpStateCode GetStatusCode()
            => Task.Document.ResponseHeader.StatusCode;

        public string GetRequestHeader(string key)
            => Task.Document.RequestHeader.HeaderParameter.TryGetValue(key, out string value) ? value : default;

        public string GetResponseHeader(string key)
            => Task.Document.ResponseHeader.HeaderParameter.TryGetValue(key, out string value) ? value : default;

        public IEnumerable<(string, HttpCookie.Cookie)> GetAddedCookies()
            => Task.Document.RequestHeader.Cookie.AddedCookies
                .Select(p => (p.Key, p.Value));

        public HttpRequestHeader Request
            => Task.Document.RequestHeader;

        public HttpResponseHeader Response
            => Task.Document.ResponseHeader;
    }
}
