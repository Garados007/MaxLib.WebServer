using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestHttpStreamDataSource
    {
        [TestMethod]
        public async Task TestWriteStreamWithACountOnlyReadsAsMuchAsRequested()
        {
            // only `count` bytes may be read from the source, not everything up to its end
            var source = new byte[100_000];
            for (var i = 0; i < source.Length; ++i)
                source[i] = (byte)i;
            using var sourceStream = new MemoryStream(source);
            using var dataSource = new HttpStreamDataSource(sourceStream);
            using var output = new MemoryStream();

            var written = await dataSource.WriteStream(output, 0, 10).ConfigureAwait(false);

            Assert.AreEqual(10, written);
            Assert.AreEqual(10, output.Length);
            Assert.IsTrue(sourceStream.Position <= 10,
                $"expected the source stream to be read no further than position 10, but it's at {sourceStream.Position}");
        }

        [TestMethod]
        public async Task TestWriteStreamWithNoCountReadsTheWholeStream()
        {
            var source = new byte[1000];
            for (var i = 0; i < source.Length; ++i)
                source[i] = (byte)i;
            using var sourceStream = new MemoryStream(source);
            using var dataSource = new HttpStreamDataSource(sourceStream);
            using var output = new MemoryStream();

            var written = await dataSource.WriteStream(output, 0, null).ConfigureAwait(false);

            Assert.AreEqual(1000, written);
            CollectionAssert.AreEqual(source, output.ToArray());
        }
    }
}
