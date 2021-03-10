using MaxLib.WebServer.Services;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            test.Request.HttpProtocol = HttpProtocollDefinition.HttpVersion1_1;
            await new HttpResponseCreator().ProgressTask(test.Task).ConfigureAwait(false);
            Assert.AreEqual($"{MimeType.TextPlain}; charset=utf-8", test.Response.FieldContentType);
            Assert.AreNotEqual(null, test.Response.FieldDate);
            Assert.AreEqual(HttpProtocollDefinition.HttpVersion1_1, test.Response.HttpProtocol);
            Assert.AreEqual("keep-alive", test.GetResponseHeader("Connection"));
            Assert.AreEqual("IE=Edge", test.GetResponseHeader("X-UA-Compatible"));
            Assert.AreEqual("4", test.GetResponseHeader("Content-Length"));
        }
    }
}
