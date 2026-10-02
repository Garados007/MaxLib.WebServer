using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MaxLib.WebServer.IO;
using MaxLib.WebServer.Post;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MaxLib.WebServer.Test.PostData
{
    [TestClass]
    public class TestRawPostData
    {
        // Stream.ReadAsync may return fewer bytes than requested; deliver one byte at a time to make sure
        // SetAsync doesn't truncate the body.
        private sealed class OneByteAtATimeStream(byte[] data) : MemoryStream(data)
        {
            public override ValueTask<int> ReadAsync(System.Memory<byte> buffer,
                CancellationToken cancellationToken = default)
                => base.ReadAsync(buffer.Length > 1 ? buffer[..1] : buffer, cancellationToken);
        }

        [TestMethod]
        public async Task TestSetAsyncReadsTheWholeBodyEvenWhenItArrivesOneByteAtATime()
        {
            var body = Encoding.UTF8.GetBytes("{\"hello\":\"world, this is a longer body than one byte\"}");
            using var content = new ContentStream(new NetworkReader(new OneByteAtATimeStream(body)), body.Length);
            var data = new RawPostData(MimeType.ApplicationJson);

            await data.SetAsync(new WebProgressTask(), content, "").ConfigureAwait(false);

            Assert.IsNotNull(data.Entry.Content);
            var stored = data.Entry.Content!.Value.ToArray();
            CollectionAssert.AreEqual(body, stored);
        }
    }
}
