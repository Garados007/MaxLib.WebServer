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
                await new HttpRequestParser().ProgressTask(test.Task).ConfigureAwait(false);
                Assert.AreEqual(HttpProtocolMethod.Get, test.Request.ProtocolMethod);
                Assert.AreEqual("/test.html", test.Request.Location.DocumentPath);
                Assert.AreEqual(HttpProtocolDefinition.HttpVersion1_1, test.Request.HttpProtocol);
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
                await new HttpRequestParser().ProgressTask(test.Task).ConfigureAwait(false);
                Assert.AreEqual(HttpProtocolMethod.Post, test.Request.ProtocolMethod);
                Assert.AreEqual("/test.html", test.Request.Location.DocumentPath);
                Assert.AreEqual(HttpProtocolDefinition.HttpVersion1_1, test.Request.HttpProtocol);
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
            // a real HTTP client always terminates every line - including the last one
            // before a boundary - with a literal CRLF; that CRLF belongs to the boundary
            // delimiter per RFC 2046 §5.1.1, not to the part's content, and must not show
            // up in the parsed content
            var content =
                "-----1234\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n" +
                "Hello World\r\n" +
                "-----1234--\r\n";
            var sb = new StringBuilder();
            sb.AppendLine("POST /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine($"Content-Length: {content.Length}");
            sb.AppendLine("Content-Type: multipart/form-data; boundary=---1234");
            sb.AppendLine();
            sb.Append(content);
            using (var output = test.SetStream(sb.ToString()))
            {
                await new HttpRequestParser().ProgressTask(test.Task).ConfigureAwait(false);
                Assert.AreEqual(HttpProtocolMethod.Post, test.Request.ProtocolMethod);
                Assert.AreEqual("/test.html", test.Request.Location.DocumentPath);
                Assert.AreEqual(HttpProtocolDefinition.HttpVersion1_1, test.Request.HttpProtocol);
                Assert.AreEqual("testdomain.local", test.GetRequestHeader("Host"));
                Assert.AreEqual(content.Length.ToString(), test.GetRequestHeader("Content-Length"));
                Assert.AreEqual("multipart/form-data; boundary=---1234", test.GetRequestHeader("Content-Type"));
                Assert.AreEqual(MimeType.MultipartFormData, test.Request.Post.MimeType);
                Assert.IsTrue(test.Request.Post.Data is Post.MultipartFormData);
                var data = (Post.MultipartFormData)test.Request.Post.Data;
                Assert.AreEqual(1, data.Entries.Count);
                Assert.AreEqual(1, data.Entries[0].Header.Count);
                Assert.AreEqual("text/plain", data.Entries[0].Header["Content-Type"]);
                Assert.IsNull(data.Entries[0].TempFile);
                Assert.IsTrue(data.Entries[0].Content.HasValue);
                Assert.AreEqual("Hello World",
                    Encoding.UTF8.GetString(data.Entries[0].Content.Value.ToArray())
                );
            }
        }

        [TestMethod]
        public async Task TestRequestParser_MultipartPost_BinaryContentIsNotCorrupted()
        {
            // every byte value 0x00-0x7F, including embedded CR/LF bytes that are not part
            // of a boundary delimiter - the parser must not stop early on those, and must
            // return the content byte-for-byte with no trailing CRLF appended or stripped
            var contentBytes = new byte[128];
            for (var i = 0; i < contentBytes.Length; ++i)
                contentBytes[i] = (byte)i;
            var binaryPart = new string(Array.ConvertAll(contentBytes, b => (char)b));

            var content =
                "-----1234\r\n" +
                "Content-Type: application/octet-stream\r\n" +
                "\r\n" +
                binaryPart + "\r\n" +
                "-----1234--\r\n";
            var sb = new StringBuilder();
            sb.AppendLine("POST /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine($"Content-Length: {content.Length}");
            sb.AppendLine("Content-Type: multipart/form-data; boundary=---1234");
            sb.AppendLine();
            sb.Append(content);
            using (var output = test.SetStream(sb.ToString()))
            {
                await new HttpRequestParser().ProgressTask(test.Task).ConfigureAwait(false);
                Assert.IsTrue(test.Request.Post.Data is Post.MultipartFormData);
                var data = (Post.MultipartFormData)test.Request.Post.Data;
                Assert.AreEqual(1, data.Entries.Count);
                Assert.IsTrue(data.Entries[0].Content.HasValue);
                CollectionAssert.AreEqual(contentBytes, data.Entries[0].Content.Value.ToArray());
            }
        }

        [TestMethod]
        public async Task TestRequestParser_TransferEncodingIsRejected()
        {
            var sb = new StringBuilder();
            sb.AppendLine("POST /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine("Transfer-Encoding: chunked");
            sb.AppendLine();
            sb.Append("0\r\n\r\n");
            using (var output = test.SetStream(sb.ToString()))
            {
                await new HttpRequestParser().ProgressTask(test.Task).ConfigureAwait(false);
                Assert.AreEqual(HttpStateCode.NotImplemented, test.GetStatusCode());
                // the chunked body was never read off the socket; the connection must not
                // be kept alive, or those bytes would be parsed as the next request's header
                Assert.AreEqual(HttpConnectionType.Close, test.Request.FieldConnection);
            }
        }

        [TestMethod]
        public async Task TestRequestParser_TransferEncodingWithContentLengthIsRejected()
        {
            var content = "foo=bar";
            var sb = new StringBuilder();
            sb.AppendLine("POST /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine($"Content-Length: {content.Length}");
            sb.AppendLine("Transfer-Encoding: chunked");
            sb.AppendLine("Content-Type: application/x-www-form-urlencoded");
            sb.AppendLine();
            sb.Append(content);
            using (var output = test.SetStream(sb.ToString()))
            {
                await new HttpRequestParser().ProgressTask(test.Task).ConfigureAwait(false);
                Assert.AreEqual(HttpStateCode.NotImplemented, test.GetStatusCode());
                Assert.AreEqual(HttpConnectionType.Close, test.Request.FieldConnection);
            }
        }
    }
}