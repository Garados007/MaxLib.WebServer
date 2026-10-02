using MaxLib.WebServer.IO;
using MaxLib.WebServer.Post;
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
        public async Task TestSendingStripsCrLfFromHeaderNamesAndValues()
        {
            // A header value built from attacker-influenced text (e.g. a redirect Location)
            // must never let a raw CRLF split it into extra header/response lines -
            // see http-header-crlf-injection.md
            test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            test.Response.StatusCode = HttpStateCode.OK;
            test.Response.HeaderParameter["X-Custom"] = "evil\r\nX-Injected: 1";

            using (var response = test.SetStream())
            using (var r = new StreamReader(response))
            {
                await new HttpSender().ProgressTask(test.Task).ConfigureAwait(false);

                response.Position = 0;

                Assert.AreEqual("HTTP/1.1 200 OK", r.ReadLine());
                Assert.AreEqual("X-Custom: evilX-Injected: 1", r.ReadLine());
                Assert.AreEqual("", r.ReadLine());
                Assert.AreEqual(null, r.ReadLine());
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

        // Simulates a connection reset while draining the leftover, unread POST body.
        private sealed class ResetsWhileReadingStream : Stream
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
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                => throw new IOException("simulated connection reset");
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        [TestMethod]
        public async Task TestResponseIsSentEvenWhenPostBodyDrainHitsAConnectionReset()
        {
            // a connection reset while draining the leftover POST body must be logged, not propagate
            test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            test.Response.StatusCode = HttpStateCode.OK;
            test.Response.FieldContentType = MimeType.TextPlain;
            test.Task.Document.DataSources.Add(new HttpStringDataSource("hello"));

            var content = new ContentStream(
                new NetworkReader(new ResetsWhileReadingStream()),
                1024
            );
            test.Request.Post.SetPost(test.Task, content, MimeType.ApplicationOctetStream);

            using (var response = test.SetStream())
            using (var r = new StreamReader(response))
            {
                await new HttpSender().ProgressTask(test.Task).ConfigureAwait(false);

                response.Position = 0;
                var text = r.ReadToEnd();
                StringAssert.Contains(text, "HTTP/1.1 200 OK");
                StringAssert.Contains(text, "hello");

                Assert.AreEqual(HttpConnectionType.Close, test.Request.FieldConnection);
            }
        }

        // Writes through to a MemoryStream normally, but FlushAsync always fails -
        // simulating a client that disconnects while the response is being sent.
        private sealed class ThrowsOnFlushStream : MemoryStream
        {
            public override Task FlushAsync(CancellationToken cancellationToken)
                => throw new IOException("simulated broken connection");
        }

        // Simulates a client that disconnects while the response body is being written -
        // e.g. HttpDataSource.WriteStream itself surfacing the broken connection, as
        // HttpStreamDataSource/HttpChunkedStream do when the underlying stream throws.
        private sealed class ThrowsOnWriteStreamDataSource : HttpDataSource
        {
            public override void Dispose() { }
            public override long? Length() => null;
            protected override Task<long> WriteStreamInternal(Stream stream)
                => throw new IOException("simulated broken connection");
        }

        [TestMethod]
        public async Task TestBodyWriteDisconnectIsLoggedGracefullyInsteadOfPropagatingUnhandled()
        {
            // a client disconnect mid-body-write must be logged like the header/footer flush disconnects,
            // not propagate out of ProgressTask
            test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            test.Response.StatusCode = HttpStateCode.OK;
            test.Task.Document.DataSources.Add(new ThrowsOnWriteStreamDataSource());

            using (var response = test.SetStream())
            using (var r = new StreamReader(response))
            {
                // must not throw: the IOException from WriteStream is caught inside
                // ProgressTask, not propagated to the caller
                await new HttpSender().ProgressTask(test.Task).ConfigureAwait(false);

                response.Position = 0;
                var text = r.ReadToEnd();
                StringAssert.Contains(text, "HTTP/1.1 200 OK");
            }
        }

        [TestMethod]
        public async Task TestPostTempFileIsDisposedEvenWhenSendingTheResponseFails()
        {
            // the POST temp file must be cleaned up even when the connection breaks while sending the response
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
                test.Task.NetworkStream = new ThrowsOnFlushStream();

                await new HttpSender().ProgressTask(test.Task).ConfigureAwait(false);

                Assert.AreNotEqual("", tempFilePath, "the StorageMapper hook was never invoked");
                Assert.IsFalse(File.Exists(tempFilePath),
                    "the POST temp file must not be orphaned when sending the response fails");
            }
            finally
            {
                MultipartFormData.StorageMapper = originalMapper;
            }
        }
    }
}
