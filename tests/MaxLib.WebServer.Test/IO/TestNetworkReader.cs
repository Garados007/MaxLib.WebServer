using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using MaxLib.WebServer.IO;

namespace MaxLib.WebServer.Test.IO
{
    [TestClass]
    public class TestNetworkReader
    {
        // Simulates a hostile/slow peer that never delivers more than one byte per
        // socket read, regardless of how much buffer space or data is available.
        // Used to force multi-byte UTF-8 sequences to split across many refills.
        private sealed class OneByteAtATimeStream(byte[] data) : MemoryStream(data)
        {
            public override ValueTask<int> ReadAsync(Memory<byte> buffer,
                CancellationToken cancellationToken = default)
                => base.ReadAsync(buffer.Length > 1 ? buffer[..1] : buffer, cancellationToken);
        }

        // Used to exercise the constructor's "stream is not readable" guard, which a
        // plain MemoryStream (always readable) can never trigger on its own.
        private sealed class NonReadableStream : Stream
        {
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => 0;
            public override long Position { get => 0; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        Stream baseStream;

        [TestInitialize]
        public void Init()
        {
            baseStream = new MemoryStream();
            var writer = new BinaryWriter(baseStream, Encoding.UTF8, true);
            writer.Write('\u2661'); // ♡
            writer.Write('\r');
            writer.Write('\n');
            writer.Write("foo\n".ToCharArray());
            writer.Write(new byte[]{ 0, 1, 2, 3, 4, 5, 6, 7 });
            writer.Flush();
            baseStream.Position = 0;
        }

        [TestMethod]
        public async Task TestPeek()
        {
            var reader = new NetworkReader(baseStream);
            Assert.AreEqual<char?>('\u2661', await reader.PeekCharAsync().ConfigureAwait(false));
            Assert.AreEqual<char?>('\u2661', await reader.PeekCharAsync().ConfigureAwait(false));
            Assert.AreEqual<char?>('\u2661', await reader.ReadCharAsync().ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReadLine()
        {
            var reader = new NetworkReader(baseStream);
            Assert.AreEqual<string>("\u2661", await reader.ReadLineAsync().ConfigureAwait(false));
            Assert.AreEqual<string>("foo", await reader.ReadLineAsync().ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReadBytes()
        {
            var reader = new NetworkReader(baseStream);
            await reader.ReadLineAsync().ConfigureAwait(false);
            await reader.ReadLineAsync().ConfigureAwait(false);
            var buffer = await reader.ReadBytesAsync(8).ConfigureAwait(false);
            Assert.AreEqual(
                BitConverter.ToString(new byte[]{ 0, 1, 2, 3, 4, 5, 6, 7 }), 
                BitConverter.ToString(buffer)
            );
        }

        [TestMethod]
        public async Task TestReadIntoStream()
        {
            var reader = new NetworkReader(baseStream);
            await reader.ReadLineAsync().ConfigureAwait(false);
            await reader.ReadLineAsync().ConfigureAwait(false);
            using var output = new MemoryStream();
            var read = await reader.ReadAsync(output, 8).ConfigureAwait(false);
            Assert.AreEqual(8, read);
            Assert.AreEqual(
                BitConverter.ToString(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }),
                BitConverter.ToString(output.ToArray())
            );
        }

        [TestMethod]
        public async Task TestPeekAndReadBytes()
        {
            var reader = new NetworkReader(baseStream);
            Assert.AreEqual<char?>('\u2661', await reader.PeekCharAsync().ConfigureAwait(false));
            Assert.AreEqual(
                BitConverter.ToString(new byte[] { 0xe2, 0x99, 0xa1, 0x0d, 0x0a }),
                BitConverter.ToString(await reader.ReadBytesAsync(5))
            );
        }

        [TestMethod]
        public async Task TestPeekAndBreake()
        {
            var reader = new NetworkReader(baseStream);
            Assert.AreEqual<char?>('\u2661', await reader.PeekCharAsync().ConfigureAwait(false));
            Assert.AreEqual(
                BitConverter.ToString(new byte[] { 0xe2 }),
                BitConverter.ToString(await reader.ReadBytesAsync(1))
            );
            Assert.AreEqual(
                BitConverter.ToString(new byte[] { 0x99, 0xa1, 0x0d, 0x0a }),
                BitConverter.ToString(await reader.ReadBytesAsync(4))
            );
        }

        [TestMethod]
        public async Task TestBrokenRead()
        {
            var reader = new NetworkReader(baseStream);
            // remove the first byte to kill the char
            Assert.AreEqual(
                BitConverter.ToString(new byte[] { 0xe2 }),
                BitConverter.ToString(await reader.ReadBytesAsync(1))
            );
            // read an undefined char
            Assert.AreEqual(65533, (int)(await reader.ReadCharAsync().ConfigureAwait(false)));
        }

        [TestMethod]
        public async Task TestReadUntil()
        {
            var baseStream = new MemoryStream(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            var reader = new NetworkReader(baseStream);
            var readed = await reader.ReadUntilAsync(new byte[] { 4, 5, 6 }).ConfigureAwait(false);
            Assert.AreEqual("00-01-02-03", BitConverter.ToString(readed.ToArray()));
            Assert.AreEqual("04-05-06-07-08-09", BitConverter.ToString(await reader.ReadBytesAsync(6).ConfigureAwait(false)));
        }

        [TestMethod]
        public async Task TestReadUntilAsync_MarkingSplitAcrossARefillIsStillFound()
        {
            // a marker straddling two refills (one byte per socket read here) must still be found
            using var stream = new OneByteAtATimeStream(Encoding.UTF8.GetBytes("XXBOUNDYY"));
            using var reader = new NetworkReader(stream);

            var readed = await reader.ReadUntilAsync(Encoding.UTF8.GetBytes("BOUND")).ConfigureAwait(false);

            Assert.AreEqual("XX", Encoding.UTF8.GetString(readed.ToArray()));
            // the marker itself must remain unconsumed, ready for the caller to read next
            var markerAndRemainder = await reader.ReadBytesAsync(7).ConfigureAwait(false);
            Assert.AreEqual("BOUNDYY", Encoding.UTF8.GetString(markerAndRemainder));
        }

        // Regression test for https://github.com/Garados007/MaxLib.WebServer/issues/18
        // A "\r\n" line ending advances past the '\n' without decrementing the pending
        // char count. That drift accumulates with every CRLF-terminated line and, once
        // it overruns the actually-decoded data, the scan reads stale characters left
        // over from a previous decode pass into the returned line (here: a spurious
        // trailing '\0' instead of a clean "BC").
        [TestMethod]
        public async Task TestReadLine_DoesNotDriftPastDecodedData()
        {
            // buffer size 8 forces exactly one decode pass to hold "A\r\nBC" (5 bytes),
            // leaving the rest of the char buffer at its default '\0'.
            var data = Encoding.ASCII.GetBytes("A\r\nBC");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII, false, 8);
            Assert.AreEqual("A", await reader.ReadLineAsync().ConfigureAwait(false));
            Assert.AreEqual("BC", await reader.ReadLineAsync().ConfigureAwait(false));
        }

        // Regression test for https://github.com/Garados007/MaxLib.WebServer/issues/18
        // When the byte buffer is fully drained exactly as its read offset reaches the
        // buffer's end, the buffer-compaction is skipped, so the following read targets
        // a zero-length slice. That read returns 0 and is misread as "stream closed",
        // even though more data is still available - silently truncating the stream.
        [TestMethod]
        public async Task TestReadLine_RefillsAfterBufferExactlyDrained()
        {
            // buffer size 8: the first line exactly fills the buffer with no line
            // terminator, so the next line's data only arrives on a later refill.
            var data = Encoding.ASCII.GetBytes("AAAAAAAA\r\nBBBB\r\n");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII, false, 8);
            Assert.AreEqual("AAAAAAAA", await reader.ReadLineAsync().ConfigureAwait(false));
            Assert.AreEqual("BBBB", await reader.ReadLineAsync().ConfigureAwait(false));
        }

        // A malicious or merely unlucky peer can have its "\r\n" split exactly across
        // two socket reads (the '\r' arrives at the very end of one chunk, the '\n' at
        // the start of the next). The line must still be reassembled as one clean line.
        [TestMethod]
        public async Task TestReadLine_CrLfSplitAcrossRefillBoundary()
        {
            // buffer size 3: the first refill reads exactly "AB\r", stopping right on
            // the '\r'; the '\n' only becomes available on the following refill.
            var data = Encoding.ASCII.GetBytes("AB\r\nCD");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII, false, 3);
            Assert.AreEqual("AB", await reader.ReadLineAsync().ConfigureAwait(false));
            Assert.AreEqual("CD", await reader.ReadLineAsync().ConfigureAwait(false));
        }

        // A client can send a bare '\r' that is not followed by '\n' (old Mac-style
        // line ending, or just malformed/hostile input). It must still be treated as
        // a line break without swallowing the character that follows it.
        [TestMethod]
        public async Task TestReadLine_BareCrWithoutLfIsTreatedAsLineBreak()
        {
            var data = Encoding.ASCII.GetBytes("A\rB\rC");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII, false, 8);
            Assert.AreEqual("A", await reader.ReadLineAsync().ConfigureAwait(false));
            Assert.AreEqual("B", await reader.ReadLineAsync().ConfigureAwait(false));
            Assert.AreEqual("C", await reader.ReadLineAsync().ConfigureAwait(false));
            Assert.IsNull(await reader.ReadLineAsync().ConfigureAwait(false));
        }

        // A request whose header block is immediately terminated ("\r\n\r\n" with no
        // headers at all) must yield two empty lines rather than being merged, dropped,
        // or corrupted. buffer size 4 also forces the terminating refill to happen
        // exactly when the byte buffer's read offset reaches its capacity.
        [TestMethod]
        public async Task TestReadLine_ConsecutiveBlankLines()
        {
            var data = Encoding.ASCII.GetBytes("\r\n\r\n");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII, false, 4);
            Assert.AreEqual("", await reader.ReadLineAsync().ConfigureAwait(false));
            Assert.AreEqual("", await reader.ReadLineAsync().ConfigureAwait(false));
            Assert.IsNull(await reader.ReadLineAsync().ConfigureAwait(false));
        }

        // A client sending a byte that is not valid UTF-8 (here a lone 0xFF, which is
        // never a legal lead byte) must not throw or desynchronize the reader. It
        // should decode to the replacement character and parsing of subsequent,
        // well-formed lines must continue normally.
        [TestMethod]
        public async Task TestReadLine_InvalidUtf8ByteDoesNotThrowAndKeepsSync()
        {
            var data = new byte[] { 0x41, 0xFF, 0x42, 0x0D, 0x0A, 0x43, 0x0D, 0x0A }; // "A" 0xFF "B\r\nC\r\n"
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream);
            Assert.AreEqual("A�B", await reader.ReadLineAsync().ConfigureAwait(false));
            Assert.AreEqual("C", await reader.ReadLineAsync().ConfigureAwait(false));
        }

        // A hostile peer could dribble a multi-byte UTF-8 character one byte per
        // socket read on purpose, hoping to desynchronize the decoder across many
        // refills. The character must still be reconstructed correctly as part of a
        // single line.
        [TestMethod]
        public async Task TestReadLine_MultiByteCharacterSplitByteByByteAcrossReads()
        {
            // "A" + U+1F600 (F0 9F 98 80, a 4-byte sequence) + "B\r\n"
            var data = new byte[] { 0x41, 0xF0, 0x9F, 0x98, 0x80, 0x42, 0x0D, 0x0A };
            using var stream = new OneByteAtATimeStream(data);
            var reader = new NetworkReader(stream);
            var expected = "A" + char.ConvertFromUtf32(0x1F600) + "B";
            Assert.AreEqual(expected, await reader.ReadLineAsync().ConfigureAwait(false));
        }

        // A peer that connects and immediately closes without sending anything must
        // be reported as a clean "no data", not throw or hang.
        [TestMethod]
        public async Task TestReadLine_EmptyStreamReturnsNull()
        {
            using var stream = new MemoryStream(Array.Empty<byte>());
            var reader = new NetworkReader(stream);
            Assert.IsNull(await reader.ReadLineAsync().ConfigureAwait(false));
        }

        // A malformed multipart body whose boundary marker never actually appears
        // must not hang forever or throw. All bytes up to the (never reached) EOF
        // must be returned instead.
        [TestMethod]
        public async Task TestReadUntilAsync_MarkingNeverFoundReturnsAllBytes()
        {
            var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream);
            var readed = await reader.ReadUntilAsync(new byte[] { 0xAA, 0xBB, 0xCC }).ConfigureAwait(false);
            Assert.AreEqual(BitConverter.ToString(data), BitConverter.ToString(readed.ToArray()));
        }

        // An empty marking must be a safe no-op: nothing is consumed from the stream
        // and a subsequent real read still observes the untouched data.
        [TestMethod]
        public async Task TestReadUntilAsync_EmptyMarkingReadsNothing()
        {
            var data = new byte[] { 1, 2, 3 };
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream);
            var readed = await reader.ReadUntilAsync(Array.Empty<byte>()).ConfigureAwait(false);
            Assert.AreEqual(0, readed.Length);
            Assert.AreEqual(
                BitConverter.ToString(data),
                BitConverter.ToString(await reader.ReadBytesAsync(3).ConfigureAwait(false))
            );
        }

        // --- coverage: synchronous API mirrors ---
        // The synchronous PeekChar/ReadChar/ReadLine methods (and the RefillCharBuffer/
        // RefillBuffer helpers behind them) are a fully separate code path from their
        // async counterparts and were entirely untested.

        [TestMethod]
        public void TestPeek_Sync()
        {
            var reader = new NetworkReader(baseStream);
            Assert.AreEqual<char?>('♡', reader.PeekChar());
            Assert.AreEqual<char?>('♡', reader.PeekChar());
            Assert.AreEqual<char?>('♡', reader.ReadChar());
        }

        [TestMethod]
        public void TestReadLine_Sync()
        {
            var reader = new NetworkReader(baseStream);
            Assert.AreEqual<string>("♡", reader.ReadLine());
            Assert.AreEqual<string>("foo", reader.ReadLine());
        }

        [TestMethod]
        public void TestReadChar_Sync_ReplacesInvalidByteWithReplacementChar()
        {
            var reader = new NetworkReader(baseStream);
            // remove the first byte to kill the char, same trick as TestBrokenRead
            var buffer = new byte[1];
            Assert.AreEqual(1, reader.Read(buffer, 0, 1));
            Assert.AreEqual(0xe2, buffer[0]);
            Assert.AreEqual(65533, (int)reader.ReadChar()!);
        }

        [TestMethod]
        public void TestReadLine_Sync_BareCrWithoutLfIsTreatedAsLineBreak()
        {
            var data = Encoding.ASCII.GetBytes("A\rB");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII);
            Assert.AreEqual("A", reader.ReadLine());
            Assert.AreEqual("B", reader.ReadLine());
            Assert.IsNull(reader.ReadLine());
        }

        [TestMethod]
        public void TestReadLine_Sync_LineSpanningMultipleRefills()
        {
            // mirrors TestReadLine_RefillsAfterBufferExactlyDrained, but via the sync
            // API: the terminator is only found after the partial line has already
            // been buffered into a StringBuilder from an earlier refill.
            var data = Encoding.ASCII.GetBytes("AAAAAAAA\r\nBBBB\r\n");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII, false, 8);
            Assert.AreEqual("AAAAAAAA", reader.ReadLine());
            Assert.AreEqual("BBBB", reader.ReadLine());
        }

        [TestMethod]
        public void TestReadLine_Sync_LineSpanningManyRefills()
        {
            // three refills happen before the terminator ever shows up, so the
            // StringBuilder is already non-null on the second and third append -
            // exercising the "already have a StringBuilder" side of `sb ??= ...`.
            var data = Encoding.ASCII.GetBytes("AABBCC\r\n");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII, false, 2);
            Assert.AreEqual("AABBCC", reader.ReadLine());
        }

        [TestMethod]
        public void TestReadLine_Sync_BareCrAtEndOfStreamIsTreatedAsLineBreak()
        {
            // the '\r' is the very last byte in the stream, so the lookahead for a
            // paired '\n' hits a genuine EOF rather than more buffered data.
            var data = Encoding.ASCII.GetBytes("A\r");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII);
            Assert.AreEqual("A", reader.ReadLine());
            Assert.IsNull(reader.ReadLine());
        }

        [TestMethod]
        public void TestPeekAndReadChar_Sync_EmptyStreamReturnsNull()
        {
            using var stream = new MemoryStream(Array.Empty<byte>());
            var reader = new NetworkReader(stream);
            Assert.IsNull(reader.PeekChar());
            Assert.IsNull(reader.ReadChar());
        }

        [TestMethod]
        public async Task TestPeekAndReadChar_EmptyStreamReturnsNull()
        {
            using var stream = new MemoryStream(Array.Empty<byte>());
            var reader = new NetworkReader(stream);
            Assert.IsNull(await reader.PeekCharAsync().ConfigureAwait(false));
            Assert.IsNull(await reader.ReadCharAsync().ConfigureAwait(false));
        }

        // --- coverage: Dispose / DisposeAsync lifecycle ---

        [TestMethod]
        public void TestDispose_ClosesUnderlyingStreamAndBlocksFurtherUse()
        {
            var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var reader = new NetworkReader(stream);
            reader.Dispose();
            Assert.ThrowsExactly<ObjectDisposedException>(() => stream.ReadByte());
            Assert.ThrowsExactly<ObjectDisposedException>(() => reader.ReadChar());
        }

        [TestMethod]
        public void TestDispose_LeaveOpenTrue_DoesNotDisposeStream()
        {
            var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var reader = new NetworkReader(stream, Encoding.UTF8, leaveOpen: true);
            reader.Dispose();
            Assert.AreEqual(1, stream.ReadByte());
        }

        [TestMethod]
        public async Task TestDisposeAsync_ClosesUnderlyingStream()
        {
            var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var reader = new NetworkReader(stream);
            await reader.DisposeAsync().ConfigureAwait(false);
            Assert.ThrowsExactly<ObjectDisposedException>(() => stream.ReadByte());
        }

        // --- coverage: constructor validation ---

        [TestMethod]
        public void TestConstructor_NullStreamThrows()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new NetworkReader(null!));
        }

        [TestMethod]
        public void TestConstructor_NonReadableStreamThrows()
        {
            using var stream = new NonReadableStream();
            Assert.ThrowsExactly<ArgumentException>(() => new NetworkReader(stream));
        }

        [TestMethod]
        public async Task TestConstructor_TwoArgOverload_UsesGivenEncoding()
        {
            var data = Encoding.ASCII.GetBytes("hi\r\n");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII);
            Assert.AreEqual("hi", await reader.ReadLineAsync().ConfigureAwait(false));
        }

        // --- coverage: Encoding property ---

        [TestMethod]
        public async Task TestEncodingSetter_ChangesDecoder()
        {
            var data = Encoding.ASCII.GetBytes("hi\r\n");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.UTF8);
            Assert.AreEqual(Encoding.UTF8, reader.Encoding);
            reader.Encoding = Encoding.ASCII;
            Assert.AreEqual(Encoding.ASCII, reader.Encoding);
            Assert.AreEqual("hi", await reader.ReadLineAsync().ConfigureAwait(false));
        }

        [TestMethod]
        public void TestEncodingSetter_NullThrows()
        {
            using var stream = new MemoryStream();
            var reader = new NetworkReader(stream);
            Assert.ThrowsExactly<ArgumentNullException>(() => reader.Encoding = null!);
        }

        // --- coverage: raw byte-read overloads ---
        // Read(byte[],offset,count) (the plain synchronous overload) was entirely
        // untested, and several argument-validation branches on the async byte[]
        // and Memory<byte> overloads were never exercised from the "throws" side.

        [TestMethod]
        public void TestRead_Sync_ReadsBufferedAndRemainingBytes()
        {
            var reader = new NetworkReader(baseStream);
            // ReadChar (unlike PeekChar) actually advances past the decoded char, so
            // Read() must discard it from the pending char buffer before reading raw
            // bytes, rather than silently re-serving already-consumed data.
            Assert.AreEqual<char?>('♡', reader.ReadChar());
            var buffer = new byte[5];
            var read = reader.Read(buffer, 0, 5);
            Assert.AreEqual(5, read);
            Assert.AreEqual("0D-0A-66-6F-6F", BitConverter.ToString(buffer));
        }

        [TestMethod]
        public void TestRead_Sync_ArgumentValidation()
        {
            var reader = new NetworkReader(baseStream);
            var buffer = new byte[4];
            Assert.ThrowsExactly<ArgumentNullException>(() => reader.Read(null!, 0, 1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => reader.Read(buffer, -1, 1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => reader.Read(buffer, 5, 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => reader.Read(buffer, 0, -1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => reader.Read(buffer, 2, 3));
        }

        [TestMethod]
        public async Task TestReadAsync_ByteArray_ArgumentValidation()
        {
            var reader = new NetworkReader(baseStream);
            var buffer = new byte[4];
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => reader.ReadAsync(null!, 0, 1).AsTask());
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
                () => reader.ReadAsync(buffer, -1, 1).AsTask());
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
                () => reader.ReadAsync(buffer, 5, 0).AsTask());
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
                () => reader.ReadAsync(buffer, 0, -1).AsTask());
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
                () => reader.ReadAsync(buffer, 2, 3).AsTask());
        }

        [TestMethod]
        public async Task TestReadAsync_Memory_ReadsBufferedAndRemainingBytes()
        {
            var reader = new NetworkReader(baseStream);
            // force everything into the internal byte buffer without consuming it
            Assert.IsNotNull(await reader.PeekCharAsync().ConfigureAwait(false));

            // fully satisfied from the pre-buffered bytes - no further stream read needed
            var small = new byte[3];
            Assert.AreEqual(3, await reader.ReadAsync(small.AsMemory()).ConfigureAwait(false));
            Assert.AreEqual("E2-99-A1", BitConverter.ToString(small));

            // requesting more than what's left pre-buffered must also pull from BaseStream
            var rest = new byte[20];
            var read = await reader.ReadAsync(rest.AsMemory()).ConfigureAwait(false);
            Assert.AreEqual(14, read); // 17 bytes total in the fixture, 3 already consumed
        }

        [TestMethod]
        public async Task TestReadMemoryAsync_ReturnsCorrectSlice()
        {
            var reader = new NetworkReader(baseStream);
            var mem = await reader.ReadMemoryAsync(3).ConfigureAwait(false);
            Assert.AreEqual(3, mem.Length);
            Assert.AreEqual("E2-99-A1", BitConverter.ToString(mem.ToArray()));
        }

        [TestMethod]
        public async Task TestReadBytesAsync_ShortReadTrimsResultToActualLength()
        {
            // a client that promises more data (e.g. via Content-Length) than it
            // actually sends must not get its short read padded with zero bytes.
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var reader = new NetworkReader(stream);
            var result = await reader.ReadBytesAsync(10).ConfigureAwait(false);
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual("01-02-03", BitConverter.ToString(result));
        }

        // --- coverage: ReadAsync(Stream, count) ---

        [TestMethod]
        public async Task TestReadAsync_IntoStream_ArgumentValidation()
        {
            var reader = new NetworkReader(baseStream);
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => reader.ReadAsync((Stream)null!, 1).AsTask());
            using var readOnlyTarget = new MemoryStream(new byte[] { 1, 2, 3 }, writable: false);
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => reader.ReadAsync(readOnlyTarget, 1).AsTask());
            using var target = new MemoryStream();
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
                () => reader.ReadAsync(target, -1).AsTask());
        }

        [TestMethod]
        public async Task TestReadAsync_IntoStream_StopsEarlyWhenSourceExhausted()
        {
            // a malicious/broken peer can close the connection before delivering as
            // many bytes as requested; the copy must stop cleanly instead of hanging.
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var reader = new NetworkReader(stream);
            using var target = new MemoryStream();
            var read = await reader.ReadAsync(target, 100).ConfigureAwait(false);
            Assert.AreEqual(3, read);
            Assert.AreEqual("01-02-03", BitConverter.ToString(target.ToArray()));
        }

        // --- coverage: synchronous ReadUntil, and the marking-too-large guard ---

        [TestMethod]
        public void TestReadUntil_Sync_FindsMarking()
        {
            using var stream = new MemoryStream(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            var reader = new NetworkReader(stream);
            var readed = reader.ReadUntil(new byte[] { 4, 5, 6 });
            Assert.AreEqual("00-01-02-03", BitConverter.ToString(readed.ToArray()));
            var remainder = new byte[6];
            Assert.AreEqual(6, reader.Read(remainder, 0, 6));
            Assert.AreEqual("04-05-06-07-08-09", BitConverter.ToString(remainder));
        }

        [TestMethod]
        public void TestReadUntil_Sync_MarkingSplitAcrossARefillIsStillFound()
        {
            using var stream = new OneByteAtATimeStream(Encoding.UTF8.GetBytes("XXBOUNDYY"));
            var reader = new NetworkReader(stream);

            var readed = reader.ReadUntil(Encoding.UTF8.GetBytes("BOUND"));

            Assert.AreEqual("XX", Encoding.UTF8.GetString(readed.ToArray()));
            var markerAndRemainder = new byte[7];
            Assert.AreEqual(7, reader.Read(markerAndRemainder, 0, 7));
            Assert.AreEqual("BOUNDYY", Encoding.UTF8.GetString(markerAndRemainder));
        }

        [TestMethod]
        public void TestReadUntil_Sync_MarkingNeverFoundReturnsAllBytes()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream);
            var readed = reader.ReadUntil(new byte[] { 0xAA, 0xBB });
            Assert.AreEqual(BitConverter.ToString(data), BitConverter.ToString(readed.ToArray()));
        }

        [TestMethod]
        public void TestReadUntil_Sync_EmptyMarkingReadsNothing()
        {
            var data = new byte[] { 1, 2, 3 };
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream);
            var readed = reader.ReadUntil(Array.Empty<byte>());
            Assert.AreEqual(0, readed.Length);
            var remainder = new byte[3];
            Assert.AreEqual(3, reader.Read(remainder, 0, 3));
            Assert.AreEqual(BitConverter.ToString(data), BitConverter.ToString(remainder));
        }

        [TestMethod]
        public void TestReadUntil_Sync_DiscardsPendingCharBufferBeforeSearching()
        {
            // ReadChar buffers ahead into the char buffer; ReadUntil operates on raw
            // bytes and must discard that pending, already-decoded-but-unread data
            // rather than re-serving it or losing the byte it corresponds to.
            using var stream = new MemoryStream(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            var reader = new NetworkReader(stream);
            Assert.IsNotNull(reader.ReadChar());
            var readed = reader.ReadUntil(new byte[] { 4, 5, 6 });
            Assert.AreEqual("01-02-03", BitConverter.ToString(readed.ToArray()));
        }

        [TestMethod]
        public void TestReadUntil_Sync_MarkingTooLargeForBufferThrows()
        {
            // a marking that (doubled, to guarantee room to slide the match window)
            // does not fit in the read buffer must be rejected rather than silently
            // producing wrong matches.
            using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var reader = new NetworkReader(stream, Encoding.ASCII, false, 4);
            Assert.ThrowsExactly<ArgumentException>(() => reader.ReadUntil(new byte[] { 1, 2, 3 }));
        }

        [TestMethod]
        public async Task TestReadUntilAsync_MarkingTooLargeForBufferThrows()
        {
            using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var reader = new NetworkReader(stream, Encoding.ASCII, false, 4);
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => reader.ReadUntilAsync(new byte[] { 1, 2, 3 }).AsTask());
        }

        // --- coverage: ReadLineAsync(limit, ...) edge branches ---
        // The happy path is already exercised indirectly via HttpRequestParser's
        // tests; these cover the limit-specific branches that aren't.

        [TestMethod]
        public async Task TestReadLineAsync_WithLimit_EmptyStreamReturnsNull()
        {
            using var stream = new MemoryStream(Array.Empty<byte>());
            var reader = new NetworkReader(stream);
            Assert.IsNull(await reader.ReadLineAsync(10).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReadLineAsync_NegativeLimitDelegatesToUnlimitedOverload()
        {
            var data = Encoding.ASCII.GetBytes("hello\r\n");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII);
            Assert.AreEqual("hello", await reader.ReadLineAsync(-1).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReadLineAsync_LineExceedingLimitThrowsWithinSingleRefill()
        {
            var data = Encoding.ASCII.GetBytes("abcdef\r\n");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII);
            await Assert.ThrowsExactlyAsync<ReadLineOverflowException>(
                () => reader.ReadLineAsync(3).AsTask());
        }

        [TestMethod]
        public async Task TestReadLineAsync_LineExceedingLimitThrowsAcrossRefills()
        {
            // buffer size 4: neither "AAAA" nor "BBBB" contains a terminator on its
            // own, so the overflow can only be detected once the accumulated partial
            // line is checked against the limit between refills.
            var data = Encoding.ASCII.GetBytes("AAAABBBB\r\n");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII, false, 4);
            await Assert.ThrowsExactlyAsync<ReadLineOverflowException>(
                () => reader.ReadLineAsync(6).AsTask());
        }

        [TestMethod]
        public async Task TestReadLineAsync_WithLimit_LineSpanningMultipleRefillsSucceeds()
        {
            // the terminator is only found after a partial line already accumulated
            // in a StringBuilder from an earlier refill - and the completed line
            // still fits under the limit.
            var data = Encoding.ASCII.GetBytes("AAAABB\r\n");
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII, false, 4);
            Assert.AreEqual("AAAABB", await reader.ReadLineAsync(10).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestReadLineAsync_ReturnsPartialLineOnEofWithinLimit()
        {
            var data = Encoding.ASCII.GetBytes("AB"); // no terminator, stream just ends
            using var stream = new MemoryStream(data);
            var reader = new NetworkReader(stream, Encoding.ASCII);
            Assert.AreEqual("AB", await reader.ReadLineAsync(10).ConfigureAwait(false));
        }
    }
}