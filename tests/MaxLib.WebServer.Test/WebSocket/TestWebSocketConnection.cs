using MaxLib.WebServer.WebSocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Test.WebSocket
{
    /// <summary>
    /// A minimal concrete <see cref="WebSocketConnection" />, for driving
    /// <see cref="WebSocketConnection.ReceiveLoop" />/<c>SendFrame</c> directly in tests without
    /// a real handshake or socket.
    /// </summary>
    internal sealed class TestConnection : WebSocketConnection
    {
        public List<Frame> ReceivedFrames { get; } = new();
        public List<(CloseReason? Reason, string? Info)> ReceivedCloses { get; } = new();

        public TestConnection(Stream stream) : base(stream) { }

        public Task Send(Frame frame) => SendFrame(frame);

        protected override Task ReceiveClose(CloseReason? reason, string? info)
        {
            ReceivedCloses.Add((reason, info));
            return Task.CompletedTask;
        }

        protected override Task ReceivedFrame(Frame frame)
        {
            ReceivedFrames.Add(frame);
            return Task.CompletedTask;
        }
    }

    [TestClass]
    public class TestWebSocketConnection
    {
        // A stream whose writes always fail, but only after signalling that the write has
        // started and staying in flight for a bit - long enough for a concurrent SendFrame call
        // to queue up on the connection's send lock while the first is still holding it.
        private sealed class DelayedFaultyWriteStream : Stream
        {
            private readonly TaskCompletionSource writeStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task WriteStarted => writeStarted.Task;

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => 0;
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => Task.FromResult(0);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count)
                => throw new NotSupportedException("only the async write path is exercised here");
            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                writeStarted.TrySetResult();
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
                throw new IOException("simulated network failure");
            }
        }

        [TestMethod]
        public async Task TestSendFrameReleasesItsLockEvenWhenAConcurrentWriteThrows()
        {
            // this is the crux of the fix: a caller already queued up on the send lock must not
            // be left hanging forever just because another in-flight send happened to fail
            var stream = new DelayedFaultyWriteStream();
            var connection = new TestConnection(stream);
            await connection.ReceiveLoop().ConfigureAwait(false); // EOF immediately; releases the send lock once

            var first = connection.Send(new Frame { OpCode = OpCode.Binary, Payload = new byte[] { 1 } });
            await stream.WriteStarted.ConfigureAwait(false); // first now holds the lock, mid-write

            // queues up on the lock while `first` is still in flight - exactly the race that
            // used to leak the lock forever
            var second = connection.Send(new Frame { OpCode = OpCode.Binary, Payload = new byte[] { 2 } });

            var both = Task.WhenAll(first, second);
            var winner = await Task.WhenAny(both, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
            Assert.AreSame(both, winner,
                "SendFrame must release its lock even when a concurrent write throws");
        }

        [TestMethod]
        public void TestTryReassembleFragmentedPayloadConcatenatesFragmentsInOrder()
        {
            var queue = new Queue<Memory<byte>>();
            queue.Enqueue(new byte[] { 1, 2, 3 });
            queue.Enqueue(new byte[] { 4, 5 });

            var result = WebSocketConnection.TryReassembleFragmentedPayload(queue);

            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, result!.Value.ToArray());
            Assert.AreEqual(0, queue.Count, "the queue should be fully drained on success");
        }

        [TestMethod]
        public void TestTryReassembleFragmentedPayloadRejectsOversizedAccumulationInsteadOfCrashing()
        {
            // Two fragments individually far below any single-object allocation limit, but
            // whose combined length crosses int.MaxValue - the "many small fragments" route
            // described in ws-unbounded-fragment-accumulation-dos, reproduced here with just
            // two frames instead of an actual multi-gigabyte transfer. Both queue entries
            // share the same backing array (the rejection path never reads it, only sums
            // lengths), so this only allocates it once.
            var chunk = new byte[(int.MaxValue / 2) + 2];
            var queue = new Queue<Memory<byte>>();
            queue.Enqueue(chunk);
            queue.Enqueue(chunk);

            // this used to throw an unhandled OverflowException from `new byte[maxSize]`
            // instead of returning - see ws-fragment-reassembly-overflow-crash.md
            var result = WebSocketConnection.TryReassembleFragmentedPayload(queue);

            Assert.IsNull(result);
            Assert.AreEqual(2, queue.Count,
                "a rejected queue is left untouched for the caller to still inspect");
        }
    }
}
