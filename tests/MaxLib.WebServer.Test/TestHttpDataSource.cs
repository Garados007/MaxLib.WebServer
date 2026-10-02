using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestHttpDataSource
    {
        // Signals when WriteStreamInternal runs, without touching the destination stream, to separate
        // TransformToStream's worker task from BufferedSinkStream's own timing quirks.
        private sealed class SignalingDataSource(ManualResetEventSlim signal) : HttpDataSource
        {
            public override void Dispose() => GC.SuppressFinalize(this);
            public override long? Length() => null;
            protected override Task<long> WriteStreamInternal(Stream stream)
            {
                signal.Set();
                return Task.FromResult(0L);
            }
        }

        [TestMethod]
        public void TestTransformToStreamActuallyRunsTheWriter()
        {
            // the worker task must run; otherwise this signal never fires and the wait below times out
            using var signal = new ManualResetEventSlim(false);
            using var source = new SignalingDataSource(signal);
            using var stream = HttpDataSource.TransformToStream(source);

            Assert.IsTrue(signal.Wait(TimeSpan.FromSeconds(5)),
                "TransformToStream's worker task never started running");
        }

        // Never touches the stream it's given, like a source whose underlying I/O fails before producing any bytes.
        private sealed class ThrowingDataSource : HttpDataSource
        {
            public override void Dispose() => GC.SuppressFinalize(this);
            public override long? Length() => null;
            protected override Task<long> WriteStreamInternal(Stream stream)
                => throw new InvalidOperationException("boom from WriteStreamInternal");
        }

        [TestMethod]
        public async Task TestTransformToStreamCompletesInsteadOfHangingWhenWriteStreamThrows()
        {
            // FinishWrite() must be reached even if WriteStream throws, or the reader blocks forever
            using var source = new ThrowingDataSource();
            using var stream = HttpDataSource.TransformToStream(source);

            // Give the worker task time to run its try/catch/finally before reading; reading concurrently hits a
            // BufferedSinkStream quirk (FinishWrite() does not wake a waiting reader), as noted on SignalingDataSource.
            await Task.Delay(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);

            var buffer = new byte[16];
            var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
            var winner = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(4))).ConfigureAwait(false);

            Assert.AreSame(readTask, winner,
                "TransformToStream must let the reader observe completion once WriteStream throws, instead of hanging forever");
            // the read must surface the failure instead of reporting a clean EOF
            var ex = await Assert.ThrowsExactlyAsync<IOException>(() => readTask).ConfigureAwait(false);
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
        public async Task TestTransformToStreamSurfacesAWriteStreamFailureAfterPartialData()
        {
            // once partial data was written, the stream must surface the WriteStream failure after
            // the data is drained instead of ending silently
            using var source = new PartialThenThrowDataSource();
            using var stream = HttpDataSource.TransformToStream(source);

            // Give the worker task time to write, throw and call FinishWrite() before any read; see the note above.
            await Task.Delay(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);

            var buffer = new byte[256];
            var read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            Assert.AreEqual("PARTIAL-DATA-BEFORE-FAILURE", Encoding.ASCII.GetString(buffer, 0, read),
                "the bytes written before the failure must still be delivered");

            var ex = await Assert.ThrowsExactlyAsync<IOException>(
                () => stream.ReadAsync(buffer, 0, buffer.Length)).ConfigureAwait(false);
            Assert.IsInstanceOfType(ex.InnerException, typeof(InvalidOperationException));
            Assert.AreEqual("boom after writing partial data", ex.InnerException!.Message);
        }
    }
}
