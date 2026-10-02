using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaxLib.WebServer.IO;
using MaxLib.WebServer.Services;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Services
{
    [TestClass]
    public class TestHttp404Service
    {
        // Simulates a client that stalls mid-upload: every async read blocks forever unless the
        // caller's cancellation token fires - mirrors TestHttpSender.StallingStream.
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
        public void TestProgressTaskDoesNotBlockOnAStalledBody()
        {
            // the 404 response must neither block on nor fail because of a stalled request body
            var server = new TestWebServer();
            var task = new TestTask(server) { CurrentStage = ServerStage.CreateDocument }.Task;
            var reader = new NetworkReader(new StallingStream(), null, true);
            var content = new ContentStream(reader, 1024, TimeSpan.FromMilliseconds(50));
            task.Request.Post.SetPost(task, content, "text/plain");

            var stopwatch = Stopwatch.StartNew();
            new Http404Service().ProgressTask(task).GetAwaiter().GetResult();
            stopwatch.Stop();

            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                $"ProgressTask must not block on the request body at all, took {stopwatch.Elapsed}");
            Assert.AreEqual(HttpStateCode.NotFound, task.Response.StatusCode);
            var body = ((HttpStringDataSource)task.Document.DataSources.Single()).Data;
            StringAssert.Contains(body, "[text/plain]");
        }
    }
}
