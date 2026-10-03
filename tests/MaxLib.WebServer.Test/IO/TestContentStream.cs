using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MaxLib.WebServer.IO;

namespace MaxLib.WebServer.Test.IO
{
    [TestClass]
    public class TestContentStream
    {
        // Simulates a client that stalls mid-upload: every read blocks forever unless the
        // caller's cancellation token fires. Tracks whether it was ever closed, so tests can
        // verify the underlying connection is (or isn't) torn down as expected.
        private sealed class StallingStream : Stream
        {
            public bool Disposed { get; private set; }

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
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    Disposed = true;
                base.Dispose(disposing);
            }
        }

        // Tracks whether it was ever closed, so a test can verify a keep-alive connection is
        // left open after a normal (non-cancelled) drain.
        private sealed class TrackingMemoryStream(byte[] data) : MemoryStream(data)
        {
            public bool Disposed { get; private set; }
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    Disposed = true;
                base.Dispose(disposing);
            }
        }

        private static async Task AssertCancelsQuickly(Func<Task> action)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await action().ConfigureAwait(false);
                Assert.Fail("expected an OperationCanceledException");
            }
            catch (OperationCanceledException)
            {
                // expected - any subclass (e.g. TaskCanceledException) is fine too
            }
            stopwatch.Stop();
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"expected cancellation well within the configured timeout, took {stopwatch.Elapsed}");
        }

        [TestMethod]
        public async Task TestDiscardAsyncRespectsConfiguredTimeout()
        {
            var reader = new NetworkReader(new StallingStream(), null, true);
            var content = new ContentStream(reader, 1024, TimeSpan.FromMilliseconds(50));

            await AssertCancelsQuickly(() => content.DiscardAsync()).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestReadAsyncRespectsConfiguredTimeout()
        {
            var reader = new NetworkReader(new StallingStream(), null, true);
            var content = new ContentStream(reader, 1024, TimeSpan.FromMilliseconds(50));

            await AssertCancelsQuickly(async () =>
            {
                var buffer = new byte[16];
                await content.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestDisposeAsyncRespectsConfiguredTimeout()
        {
            var reader = new NetworkReader(new StallingStream(), null, true);
            var content = new ContentStream(reader, 1024, TimeSpan.FromMilliseconds(50));

            // DisposeAsync tries to drain the remaining bytes first; on a stalled stream
            // that never arrives, so the cancellation must surface here rather than block
            await AssertCancelsQuickly(() => content.DisposeAsync().AsTask()).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestNoTimeoutConfiguredStillWorks()
        {
            // sanity check: the original two-argument constructor (no timeout) behaves
            // exactly as before against a normal, non-stalling source
            var bytes = new byte[] { 1, 2, 3, 4 };
            using var reader = new NetworkReader(new MemoryStream(bytes));
            using var content = new ContentStream(reader, bytes.Length);

            var buffer = new byte[4];
            var read = await content.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);

            Assert.AreEqual(4, read);
            CollectionAssert.AreEqual(bytes, buffer);
            Assert.AreEqual(CancellationToken.None, content.TimeoutToken);
        }

        [TestMethod]
        public async Task TestDiscardAsyncNeverClosesTheUnderlyingStreamEvenOnCancellation()
        {
            // DiscardAsync can be called mid-pipeline, before any response has been sent on
            // this connection (e.g. MultipartFormData drains trailing bytes while still
            // parsing the request) - it must never close the connection itself, or a later
            // stage could lose the ability to send a response (e.g. a timeout status)
            var stream = new StallingStream();
            var reader = new NetworkReader(stream, null, false); // leaveOpen: false
            var content = new ContentStream(reader, 1024, TimeSpan.FromMilliseconds(50));

            await AssertCancelsQuickly(() => content.DiscardAsync()).ConfigureAwait(false);

            Assert.IsFalse(stream.Disposed,
                "a plain discard must never close the underlying connection, even when cancelled");
        }

        [TestMethod]
        public async Task TestReadAsyncNeverClosesTheUnderlyingStreamEvenOnCancellation()
        {
            var stream = new StallingStream();
            var reader = new NetworkReader(stream, null, false);
            var content = new ContentStream(reader, 1024, TimeSpan.FromMilliseconds(50));

            await AssertCancelsQuickly(async () =>
            {
                var buffer = new byte[16];
                await content.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Assert.IsFalse(stream.Disposed,
                "a plain read must never close the underlying connection, even when cancelled");
        }

        [TestMethod]
        public async Task TestDisposeAsyncClosesTheUnderlyingStreamOnCancellation()
        {
            // DisposeAsync, unlike a plain read/discard, is the terminal operation on this
            // stream - callers must only invoke it once nothing further needs the
            // connection (e.g. after a response has already been sent), so it is safe (and
            // necessary) for it to close the connection immediately if its drain is
            // cancelled, rather than leaving that to whichever code further up the stack
            // happens to notice the resulting exception
            var stream = new StallingStream();
            var reader = new NetworkReader(stream, null, false);
            var content = new ContentStream(reader, 1024, TimeSpan.FromMilliseconds(50));

            await AssertCancelsQuickly(() => content.DisposeAsync().AsTask()).ConfigureAwait(false);

            Assert.IsTrue(stream.Disposed,
                "expected the underlying stream to be closed after a cancelled disposal");
        }

        [TestMethod]
        public async Task TestDiscardAsyncStopsAtEarlyEofInsteadOfBusyLooping()
        {
            // DiscardAsync must stop when the connection ends before the declared length instead of spinning.
            // No timeout is configured on purpose; the bound below turns a spin into a test failure.
            var reader = new NetworkReader(new MemoryStream(new byte[] { 1, 2, 3, 4 }), null, true);
            using var content = new ContentStream(reader, 1000);

            var discardTask = content.DiscardAsync();
            var completed = await Task.WhenAny(discardTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);

            Assert.AreSame(discardTask, completed,
                "DiscardAsync should stop at genuine EOF instead of busy-looping");
            await discardTask.ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestDiscardStopsAtEarlyEofInsteadOfBusyLooping()
        {
            var reader = new NetworkReader(new MemoryStream(new byte[] { 1, 2, 3, 4 }), null, true);
            using var content = new ContentStream(reader, 1000);

            var discardTask = Task.Run(content.Discard);
            var completed = await Task.WhenAny(discardTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);

            Assert.AreSame(discardTask, completed,
                "Discard should stop at genuine EOF instead of busy-looping");
            await discardTask.ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestDiscardAsyncLeavesTheUnderlyingStreamOpenOnNormalCompletion()
        {
            // a keep-alive connection must survive an ordinary (non-cancelled) drain, since
            // it is reused for the next request on the same connection
            var stream = new TrackingMemoryStream(new byte[] { 1, 2, 3, 4 });
            var reader = new NetworkReader(stream, null, false);
            var content = new ContentStream(reader, 4, TimeSpan.FromSeconds(30));

            await content.DiscardAsync().ConfigureAwait(false);

            Assert.IsFalse(stream.Disposed,
                "a normal drain must not close the underlying connection");
        }
    }
}
