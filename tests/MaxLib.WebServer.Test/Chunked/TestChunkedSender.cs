using MaxLib.WebServer.Chunked;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Test.Chunked
{
    [TestClass]
    public class TestChunkedSender
    {
        TestWebServer server;
        TestTask test;

        [TestInitialize]
        public void Init()
        {
            server = new TestWebServer();
            server.AddWebService(new ChunkedSender());
            test = new TestTask(server)
            {
                CurrentStage = ServerStage.SendResponse,
                TerminationStage = ServerStage.SendResponse,
            };
        }

        [TestMethod]
        public async Task TestSendingStripsCrLfFromHeaderNamesAndValues()
        {
            // A header value built from attacker-influenced text must never let a raw CRLF
            // split it into extra header/response lines - see http-header-crlf-injection.md
            test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            test.Response.StatusCode = HttpStateCode.OK;
            test.Response.HeaderParameter["X-Custom"] = "evil\r\nX-Injected: 1";

            using (var response = test.SetStream())
            using (var r = new StreamReader(response))
            {
                await new ChunkedSender().ProgressTask(test.Task).ConfigureAwait(false);

                response.Position = 0;

                Assert.AreEqual("HTTP/1.1 200 OK", r.ReadLine());
                Assert.AreEqual("X-Custom: evilX-Injected: 1", r.ReadLine());
            }
        }
    }
}
