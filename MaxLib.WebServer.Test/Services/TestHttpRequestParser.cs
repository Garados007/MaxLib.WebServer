using System.Text;
using System.Threading.Tasks;
using MaxLib.WebServer.Services;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MaxLib.WebServer.Test.Services
{
    [TestClass]
    public class TestHttpRequestParser
    {
        TestWebServer server;
        TestTask test;

        [TestInitialize]
        public void Init()
        {
            server = new TestWebServer();
            server.AddWebService(new HttpRequestParser());
            test = new TestTask(server)
            {
                CurrentStage = ServerStage.ReadRequest,
                TerminationStage = ServerStage.ReadRequest,
            };
        }

        [TestMethod]
        public async Task TestRequestParser_SimpleGet()
        {
            var sb = new StringBuilder();
            sb.AppendLine("GET /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine();
            using (var output = test.SetStream(sb.ToString()))
            {
                await new HttpRequestParser().ProgressTask(test.Task);
                Assert.AreEqual(HttpProtocollMethod.Get, test.Request.ProtocolMethod);
                Assert.AreEqual("/test.html", test.Request.Location.DocumentPath);
                Assert.AreEqual(HttpProtocollDefinition.HttpVersion1_1, test.Request.HttpProtocol);
                Assert.AreEqual("testdomain.local", test.GetRequestHeader("Host"));
            }
        }

        [TestMethod]
        public async Task TestRequestParser_SimplePost()
        {
            var content = "foo=bar&baz=foobar";
            var sb = new StringBuilder();
            sb.AppendLine("POST /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine($"Content-Length: {content.Length}");
            sb.AppendLine("Content-Type: application/x-www-form-urlencoded");
            sb.AppendLine();
            sb.Append(content);
            using (var output = test.SetStream(sb.ToString()))
            {
                await new HttpRequestParser().ProgressTask(test.Task);
                Assert.AreEqual(HttpProtocollMethod.Post, test.Request.ProtocolMethod);
                Assert.AreEqual("/test.html", test.Request.Location.DocumentPath);
                Assert.AreEqual(HttpProtocollDefinition.HttpVersion1_1, test.Request.HttpProtocol);
                Assert.AreEqual("testdomain.local", test.GetRequestHeader("Host"));
                Assert.AreEqual(content.Length.ToString(), test.GetRequestHeader("Content-Length"));
                Assert.AreEqual(MimeType.ApplicationXWwwFromUrlencoded, test.GetRequestHeader("Content-Type"));
                Assert.AreEqual(MimeType.ApplicationXWwwFromUrlencoded, test.Request.Post.MimeType);
                Assert.AreEqual(content, test.Request.Post.CompletePost);
            }
        }
    }
}