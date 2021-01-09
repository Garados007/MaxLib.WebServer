using System;
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
                Assert.IsTrue(test.Request.Post.Data is Post.UrlEncodedData);
                var data = (Post.UrlEncodedData)test.Request.Post.Data;
                Assert.AreEqual("bar", data.Parameter["foo"]);
                Assert.AreEqual("foobar", data.Parameter["baz"]);
            }
        }

        [TestMethod]
        public async Task TestRequestParser_MultipartPost()
        {
            var sb = new StringBuilder();
            sb.AppendLine("-----1234");
            sb.AppendLine("Content-Type: text/plain");
            sb.AppendLine();
            sb.Append("Hello World");
            sb.AppendLine("-----1234--");
            var content = sb.ToString();
            sb.Clear();
            sb.AppendLine("POST /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine($"Content-Length: {content.Length}");
            sb.AppendLine("Content-Type: multipart/form-data; boundary=---1234");
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
                Assert.AreEqual("multipart/form-data; boundary=---1234", test.GetRequestHeader("Content-Type"));
                Assert.AreEqual(MimeType.MultipartFormData, test.Request.Post.MimeType);
                Assert.IsTrue(test.Request.Post.Data is Post.MultipartFormData);
                var data = (Post.MultipartFormData)test.Request.Post.Data;
                Assert.AreEqual(1, data.Entries.Count);
                Assert.AreEqual(1, data.Entries[0].Header.Count);
                Assert.AreEqual("text/plain", data.Entries[0].Header["Content-Type"]);
                Assert.AreEqual("Hello World",
                    Encoding.UTF8.GetString(data.Entries[0].Content.ToArray())
                );
            }
        }
    }
}