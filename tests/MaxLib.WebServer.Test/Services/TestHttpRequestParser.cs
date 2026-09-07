using System;
using System.IO;
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
        public async Task TestRequestParser_MultipartPost_TempFileIsDeletedOnDispose()
        {
            var content =
                "-----1234\r\n" +
                "Content-Disposition: form-data; name=\"file\"; filename=\"test.txt\"\r\n" +
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
                Assert.IsTrue(test.Request.Post.Data is Post.MultipartFormData);
                var data = (Post.MultipartFormData)test.Request.Post.Data;
                Assert.AreEqual(1, data.Entries.Count);
                var entry = data.Entries[0];
                Assert.IsTrue(entry is Post.MultipartFormData.FormDataFile);
                Assert.IsNotNull(entry.TempFile);
                Assert.IsTrue(entry.TempFile!.Exists);
                var path = entry.TempFile.FullName;

                // this is what HttpResponseCreator triggers (via HttpPost.Dispose()) once
                // the response has been fully sent - the temp file must not be left behind
                data.Dispose();

                Assert.IsFalse(File.Exists(path));
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

        [TestMethod]
        public async Task TestRequestParser_ContentLengthExceedingMaxIsRejectedWithoutCallback()
        {
            var parser = new HttpRequestParser { MaxContentLength = 10 };
            var sb = new StringBuilder();
            sb.AppendLine("POST /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine("Content-Length: 1000");
            sb.AppendLine("Content-Type: application/x-www-form-urlencoded");
            sb.AppendLine();
            using (var output = test.SetStream(sb.ToString()))
            {
                await parser.ProgressTask(test.Task).ConfigureAwait(false);
                Assert.AreEqual(HttpStateCode.RequestEntityTooLarge, test.GetStatusCode());
                // the body was never read, so the connection can't be reused
                Assert.AreEqual(HttpConnectionType.Close, test.Request.FieldConnection);
            }
        }

        [TestMethod]
        public async Task TestRequestParser_ContentLengthCallbackCanRaiseTheLimit()
        {
            long? observedLength = null;
            var parser = new HttpRequestParser
            {
                MaxContentLength = 3,
                ContentLengthLimitExceeded = (_, declaredLength) =>
                {
                    observedLength = declaredLength;
                    return new ValueTask<long?>(100L);
                },
            };
            var content = "foo=bar";
            var sb = new StringBuilder();
            sb.AppendLine("POST /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine($"Content-Length: {content.Length}");
            sb.AppendLine("Content-Type: application/x-www-form-urlencoded");
            sb.AppendLine();
            sb.Append(content);
            using (var output = test.SetStream(sb.ToString()))
            {
                await parser.ProgressTask(test.Task).ConfigureAwait(false);
                Assert.AreEqual(content.Length, observedLength);
                Assert.AreNotEqual(HttpStateCode.RequestEntityTooLarge, test.GetStatusCode());
                Assert.IsTrue(test.Request.Post.Data is Post.UrlEncodedData);
                var data = (Post.UrlEncodedData)test.Request.Post.Data;
                Assert.AreEqual("bar", data.Parameter["foo"]);
            }
        }

        [TestMethod]
        public async Task TestRequestParser_ContentLengthCallbackCanStillRejectAfterRaisingTheLimit()
        {
            var parser = new HttpRequestParser
            {
                MaxContentLength = 3,
                // still below the declared Content-Length of 7
                ContentLengthLimitExceeded = (_, _) => new ValueTask<long?>(5L),
            };
            var content = "foo=bar";
            var sb = new StringBuilder();
            sb.AppendLine("POST /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine($"Content-Length: {content.Length}");
            sb.AppendLine("Content-Type: application/x-www-form-urlencoded");
            sb.AppendLine();
            sb.Append(content);
            using (var output = test.SetStream(sb.ToString()))
            {
                await parser.ProgressTask(test.Task).ConfigureAwait(false);
                Assert.AreEqual(HttpStateCode.RequestEntityTooLarge, test.GetStatusCode());
                Assert.AreEqual(HttpConnectionType.Close, test.Request.FieldConnection);
            }
        }

        [TestMethod]
        public async Task TestRequestParser_ContentLengthCallbackReturningNullMeansUnlimited()
        {
            var parser = new HttpRequestParser
            {
                MaxContentLength = 3,
                ContentLengthLimitExceeded = (_, _) => new ValueTask<long?>((long?)null),
            };
            var content = "foo=bar";
            var sb = new StringBuilder();
            sb.AppendLine("POST /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine($"Content-Length: {content.Length}");
            sb.AppendLine("Content-Type: application/x-www-form-urlencoded");
            sb.AppendLine();
            sb.Append(content);
            using (var output = test.SetStream(sb.ToString()))
            {
                await parser.ProgressTask(test.Task).ConfigureAwait(false);
                Assert.AreNotEqual(HttpStateCode.RequestEntityTooLarge, test.GetStatusCode());
                Assert.IsTrue(test.Request.Post.Data is Post.UrlEncodedData);
            }
        }

        [TestMethod]
        public async Task TestRequestParser_NegativeMaxContentLengthDisablesTheLimit()
        {
            var parser = new HttpRequestParser { MaxContentLength = -1 };
            var content = "foo=bar";
            var sb = new StringBuilder();
            sb.AppendLine("POST /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine($"Content-Length: {content.Length}");
            sb.AppendLine("Content-Type: application/x-www-form-urlencoded");
            sb.AppendLine();
            sb.Append(content);
            using (var output = test.SetStream(sb.ToString()))
            {
                await parser.ProgressTask(test.Task).ConfigureAwait(false);
                Assert.AreNotEqual(HttpStateCode.RequestEntityTooLarge, test.GetStatusCode());
                Assert.IsTrue(test.Request.Post.Data is Post.UrlEncodedData);
            }
        }

        // Simulates a client that fully sends its headers, then stalls forever partway
        // through its declared body - every read past the header bytes blocks until
        // cancelled.
        private sealed class HeaderThenStallStream : Stream
        {
            private readonly MemoryStream header;

            public HeaderThenStallStream(byte[] headerBytes)
                => header = new MemoryStream(headerBytes);

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
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
                => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, System.Threading.CancellationToken cancellationToken = default)
            {
                if (header.Position < header.Length)
                    return await header.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("unreachable: Task.Delay(Infinite) only ever completes by cancellation");
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        [TestMethod]
        public async Task TestRequestParser_StalledBodyIsCancelledWithinTheConfiguredTimeout()
        {
            var content = "foo=bar";
            var sb = new StringBuilder();
            sb.AppendLine("POST /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine($"Content-Length: {content.Length}");
            sb.AppendLine("Content-Type: application/x-www-form-urlencoded");
            sb.AppendLine();
            // the header ends here; the declared body never actually arrives
            var headerBytes = Encoding.UTF8.GetBytes(sb.ToString());

            var parser = new HttpRequestParser
            {
                ContentReadBaseTimeout = TimeSpan.FromMilliseconds(50),
                MinimumContentTransferRate = 1_000_000, // keeps the size-dependent part negligible
            };
            test.SetStream(new HeaderThenStallStream(headerBytes));

            await parser.ProgressTask(test.Task).ConfigureAwait(false);
            // header parsing itself succeeds; the stall only happens once the body is
            // actually read (i.e. once a handler consumes Post.Data)
            Assert.AreNotEqual(HttpStateCode.RequestEntityTooLarge, test.GetStatusCode());

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await test.Request.Post.DataAsync!.ConfigureAwait(false);
                Assert.Fail("expected an OperationCanceledException");
            }
            catch (OperationCanceledException)
            {
                // expected - the stalled body never satisfies UrlEncodedData.SetAsync
            }
            stopwatch.Stop();

            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"expected cancellation well within the configured timeout, took {stopwatch.Elapsed}");
        }

        // Simulates a client that sends its request line, then trickles the rest of the
        // request (the classic "Slowloris" attack) - every read past the request line
        // blocks until cancelled.
        private sealed class RequestLineThenStallStream : Stream
        {
            private readonly MemoryStream requestLine;

            public RequestLineThenStallStream(byte[] requestLineBytes)
                => requestLine = new MemoryStream(requestLineBytes);

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
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
                => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, System.Threading.CancellationToken cancellationToken = default)
            {
                if (requestLine.Position < requestLine.Length)
                    return await requestLine.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("unreachable: Task.Delay(Infinite) only ever completes by cancellation");
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        [TestMethod]
        public async Task TestRequestParser_StalledHeaderReadIsCancelledWithinTheConfiguredTimeout()
        {
            // the request line arrives, but the client then trickles nothing further - the
            // classic Slowloris attack, which used to hold the connection open indefinitely
            // once WaitForData's wait for the very first byte was already satisfied
            var requestLineBytes = Encoding.UTF8.GetBytes("GET /test.html HTTP/1.1\r\n");
            var parser = new HttpRequestParser { MaxHeaderReadTime = TimeSpan.FromMilliseconds(50) };
            test.SetStream(new RequestLineThenStallStream(requestLineBytes));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await parser.ProgressTask(test.Task).ConfigureAwait(false);
            stopwatch.Stop();

            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"expected the header read to be cancelled well within the configured timeout, took {stopwatch.Elapsed}");
            Assert.AreEqual(HttpStateCode.RequestTimeOut, test.GetStatusCode());
            // the connection's position in the byte stream is unknown once a mid-header
            // read is abandoned, so it can't safely be reused for a further request
            Assert.AreEqual(HttpConnectionType.Close, test.Request.FieldConnection);
        }

        [TestMethod]
        public async Task TestRequestParser_ZeroMaxHeaderReadTimeDisablesTheTimeout()
        {
            var parser = new HttpRequestParser { MaxHeaderReadTime = TimeSpan.Zero };
            var sb = new StringBuilder();
            sb.AppendLine("GET /test.html HTTP/1.1");
            sb.AppendLine("Host: testdomain.local");
            sb.AppendLine();
            using (var output = test.SetStream(sb.ToString()))
            {
                await parser.ProgressTask(test.Task).ConfigureAwait(false);
                Assert.AreEqual(HttpProtocolMethod.Get, test.Request.ProtocolMethod);
                Assert.AreEqual("/test.html", test.Request.Location.DocumentPath);
            }
        }
    }
}