using MaxLib.WebServer.Chunked;
using MaxLib.WebServer.IO;
using MaxLib.WebServer.Post;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading;
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
        public async Task TestSendingEmitsAWellFormedSetCookieHeader()
        {
            // AddedCookies yields KeyValuePairs; the header must use cookie.Value.ToString(), not the pair's "[Key, Value]"
            test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            test.Response.StatusCode = HttpStateCode.OK;
            test.Request.Cookie.AddedCookies["session"] = new HttpCookie.Cookie("session", "abc123");

            using (var response = test.SetStream())
            using (var r = new StreamReader(response))
            {
                await new ChunkedSender().ProgressTask(test.Task).ConfigureAwait(false);

                response.Position = 0;
                var lines = new System.Collections.Generic.List<string>();
                for (var line = r.ReadLine(); line != null; line = r.ReadLine())
                    lines.Add(line);

                var expected = "Set-Cookie: " + new HttpCookie.Cookie("session", "abc123").ToString();
                CollectionAssert.Contains(lines, expected);
                Assert.IsFalse(lines.Exists(l => l.Contains('[') || l.Contains(']')),
                    "the emitted Set-Cookie line must not contain the KeyValuePair wrapper brackets");
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

        // Never touches the stream it's given, like a source whose underlying I/O fails before producing any bytes.
        private sealed class ThrowingDataSource : HttpDataSource
        {
            public override void Dispose() => GC.SuppressFinalize(this);
            public override long? Length() => null;
            protected override Task<long> WriteStreamInternal(Stream stream)
                => throw new InvalidOperationException("boom from WriteStreamInternal");
        }

        // TestTask's BidirectionalStream only overrides the synchronous Flush()/Write(), so every awaited flush
        // runs it on a background thread. Slowing it gives SendChunk's background task a head start over the
        // recursive read of the same sink; without it the read hits a BufferedSinkStream quirk (FinishWrite()
        // does not wake a waiting reader) and hangs.
        private sealed class SlowFlushStream(Stream inner) : Stream
        {
            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => inner.CanSeek;
            public override bool CanWrite => inner.CanWrite;
            public override long Length => inner.Length;
            public override long Position { get => inner.Position; set => inner.Position = value; }

            public override void Flush()
            {
                Thread.Sleep(100);
                inner.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
            public override void SetLength(long value) => inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        }

        [TestMethod]
        public async Task TestSendingAnUnboundedLengthSourceThatThrowsFaultsInsteadOfHangingOrLookingLikeSuccess()
        {
            // if WriteStream throws, the reader of the sink must still see completion instead of blocking forever,
            // and ProgressTask must fault instead of completing as an empty, successful response
            test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            test.Response.StatusCode = HttpStateCode.OK;
            test.GetDataSources().Add(new ThrowingDataSource());

            using var response = new MemoryStream();
            test.SetStream(new MemoryStream(), new SlowFlushStream(response));

            var sendTask = new ChunkedSender().ProgressTask(test.Task);
            var winner = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(4))).ConfigureAwait(false);

            Assert.AreSame(sendTask, winner,
                "ChunkedSender.ProgressTask must not hang forever when an unbounded-length source's WriteStream throws");
            var ex = await Assert.ThrowsExactlyAsync<ChunkedSender.WriteStreamFailedException>(() => sendTask)
                .ConfigureAwait(false);
            Assert.IsInstanceOfType(ex.InnerException, typeof(InvalidOperationException));
        }

        // Writes some bytes before throwing, like a source whose underlying I/O fails partway through.
        private sealed class PartialThenThrowDataSource : HttpDataSource
        {
            public override void Dispose() => GC.SuppressFinalize(this);
            public override long? Length() => null;
            protected override async Task<long> WriteStreamInternal(Stream stream)
            {
                var data = Encoding.ASCII.GetBytes("PARTIAL-DATA-BEFORE-FAILURE");
                await stream.WriteAsync(data).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                throw new InvalidOperationException("boom after writing partial data");
            }
        }

        [TestMethod]
        public async Task TestSendingAnUnboundedLengthSourceThatThrowsAfterPartialDataNeverSendsTheTerminatingChunk()
        {
            // once WriteStream failed after writing bytes, the terminating chunk must not be written,
            // so a chunked-transfer-aware client can detect the truncated response
            test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            test.Response.StatusCode = HttpStateCode.OK;
            test.GetDataSources().Add(new PartialThenThrowDataSource());

            using var response = new MemoryStream();
            test.SetStream(new MemoryStream(), new SlowFlushStream(response));

            var sendTask = new ChunkedSender().ProgressTask(test.Task);
            var winner = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(4))).ConfigureAwait(false);
            Assert.AreSame(sendTask, winner,
                "ChunkedSender.ProgressTask must not hang forever when an unbounded-length source's WriteStream throws after partial data");

            var ex = await Assert.ThrowsExactlyAsync<ChunkedSender.WriteStreamFailedException>(() => sendTask)
                .ConfigureAwait(false);
            Assert.IsInstanceOfType(ex.InnerException, typeof(InvalidOperationException));

            response.Position = 0;
            using var r = new StreamReader(response);
            var wire = r.ReadToEnd();

            StringAssert.Contains(wire, "PARTIAL-DATA-BEFORE-FAILURE",
                "the bytes written before the failure must still reach the client");
            Assert.IsFalse(wire.Contains("\r\n0\r\n\r\n", StringComparison.Ordinal),
                "the response must never be closed off with a valid terminating 0-chunk once WriteStream has failed");
        }
    }
}
