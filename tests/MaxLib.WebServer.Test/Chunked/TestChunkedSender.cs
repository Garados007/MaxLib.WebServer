using MaxLib.WebServer.Chunked;
using MaxLib.WebServer.IO;
using MaxLib.WebServer.Post;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;
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

        [TestMethod]
        public async Task TestPostTempFileIsDisposedAfterAChunkedResponse()
        {
            // ChunkedSender replaces HttpSender.ProgressTask, so it must dispose the POST data itself
            // or its temp files leak
            var originalMapper = MultipartFormData.StorageMapper;
            string tempFilePath = "";
            try
            {
                MultipartFormData.StorageMapper = (task, file) =>
                {
                    tempFilePath = ((FileStream)file).Name;
                    return file;
                };

                var content =
                    "-----1234\r\n" +
                    "Content-Type: text/plain\r\n" +
                    "Content-Disposition: form-data; name=\"f\"; filename=\"x\"\r\n" +
                    "\r\n" +
                    "Hello World\r\n" +
                    "-----1234--\r\n";
                var contentBytes = Encoding.UTF8.GetBytes(content);
                var contentStream = new ContentStream(
                    new NetworkReader(new MemoryStream(contentBytes)), contentBytes.Length);
                test.Request.Post.SetPost(test.Task, contentStream, "multipart/form-data; boundary=---1234");
                // force the lazy IPostData to actually resolve, as if some handler had
                // already read it - this is what creates the temp file in the first place
                Assert.IsNotNull(await test.Request.Post.DataAsync!.ConfigureAwait(false));

                test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
                test.Response.StatusCode = HttpStateCode.OK;

                using var response = test.SetStream();
                await new ChunkedSender().ProgressTask(test.Task).ConfigureAwait(false);

                Assert.AreNotEqual("", tempFilePath, "the StorageMapper hook was never invoked");
                Assert.IsFalse(File.Exists(tempFilePath),
                    "the POST temp file must not be orphaned once a chunked response has been sent");
            }
            finally
            {
                MultipartFormData.StorageMapper = originalMapper;
            }
        }
    }
}
