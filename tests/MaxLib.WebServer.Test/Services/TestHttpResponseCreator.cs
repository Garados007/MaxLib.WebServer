using MaxLib.WebServer.Chunked;
using MaxLib.WebServer.Services;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Test.Services
{
    [TestClass]
    public class TestHttpResponseCreator
    {
        TestWebServer server;
        TestTask test;

        [TestInitialize]
        public void Init()
        {
            server = new TestWebServer();
            server.AddWebService(new HttpResponseCreator());
            test = new TestTask(server)
            {
                CurrentStage = ServerStage.CreateResponse,
                TerminationStage = ServerStage.CreateResponse,
            };
        }

        [TestMethod]
        public async Task TestHeader()
        {
            test.Task.Document.DataSources.Add(new HttpStringDataSource("test")
            {
                MimeType = MimeType.TextPlain,
                TextEncoding = "utf-8"
            });
            test.Task.Document.PrimaryEncoding = "utf-8";
            test.Request.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            await new HttpResponseCreator().ProgressTask(test.Task).ConfigureAwait(false);
            Assert.AreEqual($"{MimeType.TextPlain}; charset=utf-8", test.Response.FieldContentType);
            Assert.AreNotEqual(null, test.Response.FieldDate);
            Assert.AreEqual(HttpProtocolDefinition.HttpVersion1_1, test.Response.HttpProtocol);
            Assert.AreEqual("keep-alive", test.GetResponseHeader("Connection"));
            Assert.AreEqual("IE=Edge", test.GetResponseHeader("X-UA-Compatible"));
            Assert.AreEqual("4", test.GetResponseHeader("Content-Length"));
        }

        [TestMethod]
        public async Task TestUnknownLengthDataSourceRejectsWithoutARegisteredChunkedSender()
        {
            // Content-Length can never be computed correctly for an unknown-length data source
            // (Sum() would silently treat it as 0) - with no chunked sender registered to send
            // it correctly instead, this must fail loudly rather than desync the connection.
            test.Response.StatusCode = HttpStateCode.OK;
            test.Task.Document.DataSources.Add(new HttpChunkedStream(new MemoryStream()));

            await new HttpResponseCreator().ProgressTask(test.Task).ConfigureAwait(false);

            Assert.AreEqual(HttpStateCode.InternalServerError, test.GetStatusCode());
            Assert.AreEqual(0, test.GetDataSources().Count);
            Assert.AreEqual("0", test.GetResponseHeader("Content-Length"));
        }

        [TestMethod]
        public async Task TestUnknownLengthDataSourceDefersToARegisteredChunkedSender()
        {
            server.AddWebService(new ChunkedSender());
            test.Response.StatusCode = HttpStateCode.OK;
            test.Task.Document.DataSources.Add(new HttpChunkedStream(new MemoryStream()));

            await new HttpResponseCreator().ProgressTask(test.Task).ConfigureAwait(false);

            Assert.AreEqual(HttpStateCode.OK, test.GetStatusCode());
            Assert.AreEqual(1, test.GetDataSources().Count);
            Assert.IsNull(test.GetResponseHeader("Content-Length"));
        }
    }
}
