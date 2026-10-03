using MaxLib.WebServer.WebSocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // Encodes a single frame exactly as a real client would send it (masked by default,
        // matching RFC 6455 - the server never sends masked frames itself, so only client-frame
        // fixtures need this).
        private static async Task<byte[]> EncodeFrameAsync(OpCode opCode, bool final, byte[] payload, bool masked = true)
        {
            using var stream = new MemoryStream();
            var frame = new Frame { OpCode = opCode, FinalFrame = final, Payload = payload };
            if (masked)
                frame.ApplyMask();
            await frame.Write(stream).ConfigureAwait(false);
            return stream.ToArray();
        }

        private static async Task<CloseReason?> DecodeSentCloseReasonAsync(byte[] sent)
        {
            using var stream = new MemoryStream(sent);
            var frame = await Frame.TryRead(stream).ConfigureAwait(false);
            if (frame == null || frame.OpCode != OpCode.Close || frame.Payload.Length < 2)
                return null;
            Frame.ToLocalByteOrder(frame.Payload.Span[0..2]);
            return (CloseReason)BitConverter.ToUInt16(frame.Payload.Span[0..2]);
        }

        [TestMethod]
        public async Task TestReceiveLoopClosesWithProtocolErrorOnAnUnmaskedFrame()
        {
            // RFC 6455 §5.1: "A server MUST close the connection upon receiving a frame that
            // is not masked."
            var input = new MemoryStream(await EncodeFrameAsync(OpCode.Text, true, new byte[] { 1 }, masked: false)
                .ConfigureAwait(false));
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(0, connection.ReceivedFrames.Count,
                "an unmasked frame must never reach the application");
            Assert.AreEqual(CloseReason.ProtocolError, await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReceiveLoopRejectsAnOversizedControlFrame()
        {
            // RFC 6455 §5.5 caps every control frame's payload at 125 bytes.
            var input = new MemoryStream(await EncodeFrameAsync(OpCode.Ping, true, new byte[126])
                .ConfigureAwait(false));
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(CloseReason.ProtocolError,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReceiveLoopAcceptsAControlFrameLargerThanASmallMaxMessageSize()
        {
            // a Ping payload between MaxMessageSize and 125 bytes is valid and must still be answered
            var pingPayload = new byte[50];
            new Random(1).NextBytes(pingPayload);
            // ApplyMask mutates the payload in place, so capture the expected value first
            var expectedPayload = (byte[])pingPayload.Clone();
            var input = new MemoryStream(await EncodeFrameAsync(OpCode.Ping, true, pingPayload).ConfigureAwait(false));
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output))
            {
                MaxMessageSize = 10,
            };

            await connection.ReceiveLoop().ConfigureAwait(false);

            using var readBack = new MemoryStream(output.ToArray());
            var sentFrame = await Frame.TryRead(readBack).ConfigureAwait(false);
            Assert.IsNotNull(sentFrame);
            Assert.AreEqual(OpCode.Pong, sentFrame!.OpCode);
            CollectionAssert.AreEqual(expectedPayload, sentFrame.Payload.ToArray());
        }

        [TestMethod]
        public async Task TestReceiveLoopRejectsAFragmentedControlFrame()
        {
            // RFC 6455 §5.4 forbids fragmenting control frames; a fragmented one must not
            // silently merge into whatever data message is being reassembled.
            var input = new MemoryStream(await EncodeFrameAsync(OpCode.Ping, final: false, new byte[] { 1 })
                .ConfigureAwait(false));
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(CloseReason.ProtocolError,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReceiveLoopRejectsAReservedDataOpcode()
        {
            // RFC 6455 §5.2: opcodes other than 0x0-0x2 and 0x8-0xA are reserved and must fail the connection
            var input = new MemoryStream(await EncodeFrameAsync((OpCode)0x3, true, new byte[] { 1 })
                .ConfigureAwait(false));
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(0, connection.ReceivedFrames.Count);
            Assert.AreEqual(CloseReason.ProtocolError,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReceiveLoopRejectsAFragmentedReservedControlOpcode()
        {
            // 0xB is in the control range (0x8-0xF) but unassigned; it must be rejected as an unknown opcode
            var input = new MemoryStream(await EncodeFrameAsync((OpCode)0xB, final: false, new byte[] { 1 })
                .ConfigureAwait(false));
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(CloseReason.ProtocolError,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReceiveLoopRejectsANonInitialFragmentWithTheWrongOpcode()
        {
            // RFC 6455 §5.4: every fragment after the first must use the Continuation opcode
            using var input = new MemoryStream();
            var first = await EncodeFrameAsync(OpCode.Binary, final: false, new byte[] { 1 }).ConfigureAwait(false);
            input.Write(first, 0, first.Length);
            var second = await EncodeFrameAsync(OpCode.Binary, final: false, new byte[] { 2 }).ConfigureAwait(false);
            input.Write(second, 0, second.Length);
            input.Position = 0;
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(0, connection.ReceivedFrames.Count);
            Assert.AreEqual(CloseReason.ProtocolError,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReceiveLoopRejectsAFinalFragmentWithTheWrongOpcode()
        {
            using var input = new MemoryStream();
            var first = await EncodeFrameAsync(OpCode.Binary, final: false, new byte[] { 1 }).ConfigureAwait(false);
            input.Write(first, 0, first.Length);
            // the final fragment must also use the Continuation opcode
            var last = await EncodeFrameAsync(OpCode.Binary, final: true, new byte[] { 2 }).ConfigureAwait(false);
            input.Write(last, 0, last.Length);
            input.Position = 0;
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(0, connection.ReceivedFrames.Count);
            Assert.AreEqual(CloseReason.ProtocolError,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestCloseTruncatesAnOversizedInfoStringToTheControlFrameLimit()
        {
            var input = new MemoryStream();
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));
            await connection.ReceiveLoop().ConfigureAwait(false); // EOF immediately; releases the send lock

            await connection.Close(CloseReason.NormalClose, new string('a', 500)).ConfigureAwait(false);

            using var readBack = new MemoryStream(output.ToArray());
            var sentFrame = await Frame.TryRead(readBack).ConfigureAwait(false);
            Assert.IsNotNull(sentFrame);
            Assert.IsTrue(sentFrame!.Payload.Length <= 125,
                $"a Close frame's payload must never exceed 125 bytes, was {sentFrame.Payload.Length}");
        }

        [TestMethod]
        public async Task TestReceiveLoopEnforcesMaxMessageSizeDuringAccumulationNotJustAtTheEnd()
        {
            // Three non-final fragments of 10 bytes each add up to 30 bytes, but a final frame
            // never arrives - this must be rejected mid-accumulation, not "eventually, once the
            // client finishes the message" (which a client controlling this DoS never will).
            using var input = new MemoryStream();
            for (var i = 0; i < 3; ++i)
            {
                // only the first fragment of a message carries its real opcode; every later
                // fragment must be a Continuation
                var opCode = i == 0 ? OpCode.Binary : OpCode.Continuation;
                var chunk = await EncodeFrameAsync(opCode, false, new byte[10]).ConfigureAwait(false);
                input.Write(chunk, 0, chunk.Length);
            }
            input.Position = 0;
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output))
            {
                MaxMessageSize = 15,
            };

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(CloseReason.TooBigMessage,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
            Assert.AreEqual(0, connection.ReceivedFrames.Count);
        }

        [TestMethod]
        public async Task TestReceiveLoopEnforcesMaxMessageFragmentsDuringAccumulation()
        {
            // Five 1-byte fragments are cheap in total bytes but each costs one queued entry -
            // a size cap alone wouldn't bound this, so the fragment count is checked too.
            using var input = new MemoryStream();
            for (var i = 0; i < 5; ++i)
            {
                // only the first fragment of a message carries its real opcode; every later
                // fragment must be a Continuation
                var opCode = i == 0 ? OpCode.Binary : OpCode.Continuation;
                var chunk = await EncodeFrameAsync(opCode, false, new byte[] { 1 }).ConfigureAwait(false);
                input.Write(chunk, 0, chunk.Length);
            }
            input.Position = 0;
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output))
            {
                MaxMessageFragments = 3,
            };

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(CloseReason.TooBigMessage,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
            Assert.AreEqual(0, connection.ReceivedFrames.Count);
        }

        [TestMethod]
        public async Task TestReceiveLoopSendsATooBigMessageCloseForAnOversizedDeclaredLength()
        {
            // A frame declaring a length > int.MaxValue is legal on the wire (RFC 6455's 64-bit
            // extended length field allows it) but can never be represented as a Memory<byte>.
            // No payload bytes are needed - Frame.TryRead(throwLargePayload: true) must reject
            // this from the header alone, before ever trying to read the (nonexistent) payload.
            var header = new byte[10];
            header[0] = 0x82; // FIN + Binary
            header[1] = 127;  // 64-bit extended length follows
            var lengthBytes = new byte[8];
            Frame.ToNetworkByteOrder(BitConverter.GetBytes((ulong)int.MaxValue + 1), lengthBytes);
            lengthBytes.CopyTo(header, 2);

            var input = new MemoryStream(header);
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(CloseReason.TooBigMessage,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReceiveLoopSendsATooBigMessageCloseForASingleFrameExceedingMaxMessageSize()
        {
            // a single unfragmented frame declaring more than MaxMessageSize must be rejected from its header alone
            var header = new byte[10];
            header[0] = 0x82; // FIN + Binary
            header[1] = 127;  // 64-bit extended length follows
            var lengthBytes = new byte[8];
            Frame.ToNetworkByteOrder(BitConverter.GetBytes((ulong)1000), lengthBytes);
            lengthBytes.CopyTo(header, 2);

            var input = new MemoryStream(header);
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output))
            {
                MaxMessageSize = 100,
            };

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(CloseReason.TooBigMessage,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
            Assert.AreEqual(0, connection.ReceivedFrames.Count);
        }

        [TestMethod]
        public async Task TestReceiveLoopRejectsASingleFrameUnderTheControlFrameFloorButOverMaxMessageSize()
        {
            // a data frame under 125 bytes but over a smaller MaxMessageSize must be rejected by ReceiveLoop itself
            var input = new MemoryStream(await EncodeFrameAsync(OpCode.Binary, true, new byte[100])
                .ConfigureAwait(false));
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output))
            {
                MaxMessageSize = 10,
            };

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(CloseReason.TooBigMessage,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
            Assert.AreEqual(0, connection.ReceivedFrames.Count);
        }

        [TestMethod]
        public async Task TestReceiveLoopRejectsAContinuationOpcodeAsTheFirstFrameOfAMessage()
        {
            // RFC 6455 §5.4: the first (or only) frame of a message must not use the Continuation opcode
            var input = new MemoryStream(await EncodeFrameAsync(OpCode.Continuation, true, new byte[] { 1 })
                .ConfigureAwait(false));
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(0, connection.ReceivedFrames.Count);
            Assert.AreEqual(CloseReason.ProtocolError,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReceiveLoopRejectsAContinuationOpcodeStartingAFragmentedMessage()
        {
            var input = new MemoryStream(await EncodeFrameAsync(OpCode.Continuation, false, new byte[] { 1 })
                .ConfigureAwait(false));
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));

            await connection.ReceiveLoop().ConfigureAwait(false);

            Assert.AreEqual(0, connection.ReceivedFrames.Count);
            Assert.AreEqual(CloseReason.ProtocolError,
                await DecodeSentCloseReasonAsync(output.ToArray()).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReceiveLoopReassemblesA3FragmentMessageWithTheFirstFragmentsOpcode()
        {
            // Per RFC 6455, only the first fragment carries the real opcode (Text/Binary);
            // every later fragment - including the final one - must be Continuation. A message
            // split into 3+ fragments used to have its opcode overwritten to Continuation by
            // the middle fragment(s).
            using var input = new MemoryStream();
            foreach (var chunk in await Task.WhenAll(
                EncodeFrameAsync(OpCode.Text, false, new byte[] { 1 }),
                EncodeFrameAsync(OpCode.Continuation, false, new byte[] { 2 }),
                EncodeFrameAsync(OpCode.Continuation, true, new byte[] { 3 })
            ).ConfigureAwait(false))
                input.Write(chunk, 0, chunk.Length);
            input.Position = 0;
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));

            await connection.ReceiveLoop().ConfigureAwait(false);

            var received = connection.ReceivedFrames.Single();
            Assert.AreEqual(OpCode.Text, received.OpCode);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, received.Payload.ToArray());
        }

        [TestMethod]
        public async Task TestReceiveLoopAcceptsAndUnmasksAProperlyMaskedFrame()
        {
            var input = new MemoryStream(await EncodeFrameAsync(OpCode.Text, true, new byte[] { 1, 2, 3 })
                .ConfigureAwait(false));
            var output = new MemoryStream();
            var connection = new TestConnection(new WebServerTaskCreator.BidirectionalStream(input, output));

            await connection.ReceiveLoop().ConfigureAwait(false);

            var received = connection.ReceivedFrames.Single();
            Assert.IsFalse(received.HasMaskingKey, "the payload must already be unmasked by the time it's delivered");
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, received.Payload.ToArray());
        }

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

        // Records every byte written, succeeding immediately - used to inspect exactly what
        // ended up on the wire.
        private sealed class RecordingStream : Stream
        {
            public MemoryStream Written { get; } = new();

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
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                Written.Write(buffer.Span);
                return ValueTask.CompletedTask;
            }
        }

        [TestMethod]
        public async Task TestSendFrameDoesNotSendTwoCloseFramesWhenTwoClosesRaceForTheLock()
        {
            // the send lock is only released by ReceiveLoop's first action, so both Send calls suspend on it;
            // the call granted the lock first must set SendCloseSignal before the other passes its re-check
            var stream = new RecordingStream();
            var connection = new TestConnection(stream);
            var closePayload = new byte[] { 0x03, 0xE8 };

            var first = connection.Send(new Frame { OpCode = OpCode.Close, Payload = closePayload });
            var second = connection.Send(new Frame { OpCode = OpCode.Close, Payload = closePayload });

            await connection.ReceiveLoop().ConfigureAwait(false); // EOF immediately; releases the lock once
            await Task.WhenAll(first, second).ConfigureAwait(false);

            stream.Written.Position = 0;
            var frame1 = await Frame.TryRead(stream.Written).ConfigureAwait(false);
            Assert.IsNotNull(frame1, "the first Close frame must still be sent");
            Assert.AreEqual(OpCode.Close, frame1!.OpCode);
            var frame2 = await Frame.TryRead(stream.Written).ConfigureAwait(false);
            Assert.IsNull(frame2, "a second Close frame must never be sent once the first has been");
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
