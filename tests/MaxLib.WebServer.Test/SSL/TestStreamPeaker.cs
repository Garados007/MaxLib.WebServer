using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MaxLib.WebServer.SSL;

namespace MaxLib.WebServer.Test.SSL
{
    [TestClass]
    public class TestStreamPeaker
    {
        // Simulates a socket that delivers its payload in small fragments instead of
        // handing back everything in one Read() call, and reports a graceful close
        // (Read() returning 0) once the payload is exhausted.
        class FragmentedStream : Stream
        {
            readonly byte[] data;
            readonly int maxChunk;
            int position;

            public FragmentedStream(byte[] data, int maxChunk)
            {
                this.data = data;
                this.maxChunk = maxChunk;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var remaining = data.Length - position;
                if (remaining == 0)
                    return 0;
                var length = Math.Min(Math.Min(count, maxChunk), remaining);
                Array.Copy(data, position, buffer, offset, length);
                position += length;
                return length;
            }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        [TestMethod]
        public void TestFirstByteIsPeekedNotConsumed()
        {
            var peaker = new DualSecureWebServer.StreamPeaker(new FragmentedStream(new byte[] { 0x16, 0x03, 0x01 }, 16));
            Assert.IsTrue(peaker.HasFirstByte);
            Assert.AreEqual((byte)0x16, peaker.FirstByte);
            // reading FirstByte again must not advance the underlying stream
            Assert.AreEqual((byte)0x16, peaker.FirstByte);

            var buffer = new byte[3];
            var read = peaker.Read(buffer, 0, buffer.Length);
            Assert.AreEqual(3, read);
            CollectionAssert.AreEqual(new byte[] { 0x16, 0x03, 0x01 }, buffer);
        }

        [TestMethod]
        public void TestReadForwardsFragmentedUnderlyingReads()
        {
            var payload = new byte[] { 0x47, 0x45, 0x54, 0x20, 0x2f, 0x20, 0x48, 0x54, 0x54, 0x50 }; // "GET / HTTP"
            // force the underlying stream to hand back at most 2 bytes per Read() call
            var peaker = new DualSecureWebServer.StreamPeaker(new FragmentedStream(payload, 2));

            Assert.AreEqual((byte)'G', peaker.FirstByte);

            var result = new byte[payload.Length];
            var total = 0;
            while (total < result.Length)
            {
                var read = peaker.Read(result, total, result.Length - total);
                Assert.IsTrue(read > 0, "Read() returned 0 before all data was consumed");
                total += read;
            }

            CollectionAssert.AreEqual(payload, result);
        }

        [TestMethod]
        public void TestEmptyStreamDoesNotFakeAZeroByte()
        {
            var peaker = new DualSecureWebServer.StreamPeaker(new FragmentedStream(Array.Empty<byte>(), 16));

            // Before the fix, an immediately-closed connection was indistinguishable from
            // one whose first byte genuinely is 0x00 - FirstByte would silently report 0.
            Assert.IsFalse(peaker.HasFirstByte);
            Assert.AreEqual((byte)0, peaker.FirstByte);

            var buffer = new byte[4];
            var read = peaker.Read(buffer, 0, buffer.Length);
            Assert.AreEqual(0, read, "Read() must report end-of-stream instead of hanging or fabricating data");
        }

        [TestMethod]
        public void TestLiteralZeroFirstByteIsStillReadCorrectly()
        {
            // a peer that legitimately sends 0x00 as its first byte must still round-trip
            // correctly and must be distinguishable from the empty-stream case above.
            var peaker = new DualSecureWebServer.StreamPeaker(new FragmentedStream(new byte[] { 0x00, 0x01 }, 16));

            Assert.IsTrue(peaker.HasFirstByte);
            Assert.AreEqual((byte)0, peaker.FirstByte);

            var buffer = new byte[2];
            var read = peaker.Read(buffer, 0, buffer.Length);
            Assert.AreEqual(2, read);
            CollectionAssert.AreEqual(new byte[] { 0x00, 0x01 }, buffer);
        }

        [TestMethod]
        public void TestSingleByteReadAfterPeek()
        {
            var peaker = new DualSecureWebServer.StreamPeaker(new FragmentedStream(new byte[] { 0xAA, 0xBB }, 16));
            Assert.AreEqual((byte)0xAA, peaker.FirstByte);

            var buffer = new byte[1];
            Assert.AreEqual(1, peaker.Read(buffer, 0, 1));
            Assert.AreEqual((byte)0xAA, buffer[0]);

            Assert.AreEqual(1, peaker.Read(buffer, 0, 1));
            Assert.AreEqual((byte)0xBB, buffer[0]);

            Assert.AreEqual(0, peaker.Read(buffer, 0, 1));
        }
    }
}
