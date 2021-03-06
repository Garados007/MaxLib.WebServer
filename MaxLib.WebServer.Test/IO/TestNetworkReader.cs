using System;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using MaxLib.WebServer.IO;

namespace MaxLib.WebServer.Test.IO
{
    [TestClass]
    public class TestNetworkReader
    {
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
    }
}