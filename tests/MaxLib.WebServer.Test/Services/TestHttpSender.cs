using MaxLib.WebServer.IO;
using MaxLib.WebServer.Services;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Test.Services
{
    [TestClass]
    public class TestHttpSender
    {
        TestWebServer server;
        TestTask test;

        [TestInitialize]
        public void Init()
        {
            server = new TestWebServer();
            server.AddWebService(new HttpSender());
            test = new TestTask(server)
            {
                CurrentStage = ServerStage.SendResponse,
                TerminationStage = ServerStage.SendResponse,
            };
        }

        [TestMethod]
        public async Task TestSending()
        {
            test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            test.Response.StatusCode = HttpStateCode.OK;
            test.Response.FieldContentType = MimeType.TextPlain;
            test.Request.Cookie.AddedCookies.Add("test",
                new HttpCookie.Cookie("test", "value"));
            test.Task.Document.DataSources.Add(
                new HttpStringDataSource("foobarbaz\r\n"));

            using (var response = test.SetStream())
            using (var r = new StreamReader(response))
            {
                await new HttpSender().ProgressTask(test.Task).ConfigureAwait(false);

                response.Position = 0;

                Assert.AreEqual("HTTP/1.1 200 OK", r.ReadLine());
                Assert.AreEqual("Content-Type: text/plain", r.ReadLine());
                Assert.AreEqual("Set-Cookie: test=value;Path=", r.ReadLine());
                Assert.AreEqual("", r.ReadLine());
                Assert.AreEqual("foobarbaz", r.ReadLine());
            }
        }

        [TestMethod]
        public async Task TestUnconsumedPostBodyIsDrainedAfterTheResponseIsSent()
        {
            // e.g. a request that routes to a 404 or GET-style handler that never reads
            // task.Request.Post.Data at all
            var body = Encoding.UTF8.GetBytes("foo=bar");
            var content = new ContentStream(new NetworkReader(new MemoryStream(body)), body.Length);
            test.Request.Post.SetPost(test.Task, content, MimeType.ApplicationXWwwFromUrlencoded);
            test.Request.FieldConnection = HttpConnectionType.KeepAlive;

            test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            test.Response.StatusCode = HttpStateCode.OK;

            using (var response = test.SetStream())
            {
                await new HttpSender().ProgressTask(test.Task).ConfigureAwait(false);

                Assert.AreEqual(0, content.UnreadData);
                // disposal succeeded without hitting the configured timeout, so the
                // connection must be left exactly as the caller set it up
                Assert.AreEqual(HttpConnectionType.KeepAlive, test.Request.FieldConnection);
            }
        }

        [TestMethod]
        public async Task TestKeepAliveConnectionStreamSurvivesDisposalOfARequestWithABody()
        {
            // HttpRequestParser wraps the *actual, shared* connection stream in the
            // NetworkReader that ends up owned by the request's ContentStream. Disposing
            // that ContentStream after the response is sent (see above) must not take the
            // connection stream down with it, or every Keep-Alive connection would die
            // after its first request with a body.
            var body = "foo=bar";
            var requestBytes = Encoding.UTF8.GetBytes(
                "POST /test.html HTTP/1.1\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "\r\n" +
                body
            );
            using var stream = new MemoryStream();
            stream.Write(requestBytes, 0, requestBytes.Length);
            stream.Position = 0;
            test.SetStream(stream);

            test.CurrentStage = ServerStage.ReadRequest;
            await new HttpRequestParser().ProgressTask(test.Task).ConfigureAwait(false);

            test.Request.FieldConnection = HttpConnectionType.KeepAlive;
            test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            test.Response.StatusCode = HttpStateCode.OK;

            await new HttpSender().ProgressTask(test.Task).ConfigureAwait(false);

            Assert.AreEqual(HttpConnectionType.KeepAlive, test.Request.FieldConnection);
            Assert.IsTrue(stream.CanRead,
                "the connection stream must stay open for further requests on a Keep-Alive connection");
        }

        // Simulates a client that stalls mid-upload: every read blocks forever unless the
        // caller's cancellation token fires.
        private sealed class StallingStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => 0;
                set => throw new NotSupportedException();
            }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count)
                => throw new NotSupportedException("only the async read paths are exercised here");
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("unreachable: Task.Delay(Infinite) only ever completes by cancellation");
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        [TestMethod]
        public async Task TestResponseIsSentEvenWhenPostBodyDisposalTimesOut()
        {
            // this is the crux of the fix: a stalled, still-unread POST body must never
            // prevent (or race with) transmitting the response that was already prepared
            // for this request - only after that response is fully sent is it safe to
            // drain the leftover body and, if that stalls, close the connection
            test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            test.Response.StatusCode = HttpStateCode.OK;
            test.Response.FieldContentType = MimeType.TextPlain;
            test.Task.Document.DataSources.Add(new HttpStringDataSource("hello"));

            var content = new ContentStream(
                new NetworkReader(new StallingStream(), null, true),
                1024,
                TimeSpan.FromMilliseconds(50)
            );
            test.Request.Post.SetPost(test.Task, content, MimeType.ApplicationOctetStream);

            using (var response = test.SetStream())
            using (var r = new StreamReader(response))
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                await new HttpSender().ProgressTask(test.Task).ConfigureAwait(false);
                stopwatch.Stop();

                Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                    $"expected disposal to give up well within the configured timeout, took {stopwatch.Elapsed}");

                response.Position = 0;
                var text = r.ReadToEnd();
                StringAssert.Contains(text, "HTTP/1.1 200 OK");
                StringAssert.Contains(text, "hello");

                // the socket is now at an unknown position in the byte stream and must not
                // be reused for a further request
                Assert.AreEqual(HttpConnectionType.Close, test.Request.FieldConnection);
            }
        }
    }
}
