using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestHttpPartialSource
    {
        [TestMethod]
        public async Task TestWritesRequestedRangeOnly()
        {
            using var source = new HttpStringDataSource("0123456789");
            using var partial = new HttpPartialSource(source, 2, 5);

            using var destination = new MemoryStream();
            await partial.WriteStream(destination).ConfigureAwait(false);

            Assert.AreEqual("23456", Encoding.UTF8.GetString(destination.ToArray()));
        }

        [TestMethod]
        public async Task TestDestinationStreamStaysOpenAfterWrite()
        {
            // WriteStreamInternal wraps the destination in a private StreamWindow that is
            // disposed once writing finishes (fixes CA2000) - that must not close/dispose
            // the caller-owned destination stream itself.
            using var source = new HttpStringDataSource("hello world");
            using var partial = new HttpPartialSource(source, 0, null);

            using var destination = new MemoryStream();
            await partial.WriteStream(destination).ConfigureAwait(false);

            Assert.IsTrue(destination.CanWrite);
            destination.WriteByte((byte)'!');
            Assert.AreEqual("hello world!", Encoding.UTF8.GetString(destination.ToArray()));
        }

        [TestMethod]
        public async Task TestWritesRequestedRangeOnlyWithSeekableBaseSource()
        {
            // BaseSource being an HttpStreamDataSource takes the other WriteStreamInternal
            // branch, which wraps the destination in its own separately-fixed StreamWindow.
            using var baseStream = new MemoryStream(Encoding.UTF8.GetBytes("0123456789"));
            using var source = new HttpStreamDataSource(baseStream);
            using var partial = new HttpPartialSource(source, 2, 5);

            using var destination = new MemoryStream();
            await partial.WriteStream(destination).ConfigureAwait(false);

            Assert.AreEqual("23456", Encoding.UTF8.GetString(destination.ToArray()));
        }

        [TestMethod]
        public async Task TestWritesRemainderWhenCountIsNull()
        {
            using var source = new HttpStringDataSource("0123456789");
            using var partial = new HttpPartialSource(source, 7, null);

            using var destination = new MemoryStream();
            await partial.WriteStream(destination).ConfigureAwait(false);

            Assert.AreEqual("789", Encoding.UTF8.GetString(destination.ToArray()));
        }
    }
}
