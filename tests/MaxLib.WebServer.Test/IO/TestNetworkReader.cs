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
    }
}