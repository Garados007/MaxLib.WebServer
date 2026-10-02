using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MaxLib.WebServer.IO;
using MaxLib.WebServer.Post;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

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

        // Reads one byte (so the temp file gets created), then throws on every further read.
        private sealed class ThrowsAfterFirstReadStream(byte[] data) : MemoryStream(data)
        {
            public override async ValueTask<int> ReadAsync(System.Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                if (Position == 0)
                    return await base.ReadAsync(buffer.Length > 1 ? buffer[..1] : buffer, cancellationToken)
                        .ConfigureAwait(false);
                throw new IOException("simulated stalled upload");
            }
        }

        [TestMethod]
        public async Task TestSetAsyncDeletesTheOrphanedTempFileWhenTheCopyThrowsPartway()
        {
            var originalLimit = RawPostData.MaximumCacheSize;
            var originalMapper = MultipartFormData.StorageMapper;
            string? tempFilePath = null;
            try
            {
                RawPostData.MaximumCacheSize = 1; // force the temp-file branch regardless of body size
                MultipartFormData.StorageMapper = (task, file) =>
                {
                    tempFilePath = ((FileStream)file).Name;
                    return file;
                };
                var body = Encoding.UTF8.GetBytes("this body is definitely larger than one byte");
                var content = new ContentStream(new NetworkReader(new ThrowsAfterFirstReadStream(body)), body.Length);
                var data = new RawPostData(MimeType.ApplicationOctetStream);

                await Assert.ThrowsExactlyAsync<IOException>(
                    () => data.SetAsync(new WebProgressTask(), content, "")
                ).ConfigureAwait(false);

                Assert.IsNotNull(tempFilePath);
                Assert.IsFalse(File.Exists(tempFilePath),
                    "the temp file must not be orphaned when the copy into it fails partway through");
            }
            finally
            {
                RawPostData.MaximumCacheSize = originalLimit;
                MultipartFormData.StorageMapper = originalMapper;
            }
        }

        [TestMethod]
        public async Task TestSetAsyncResolvesADeclaredNonUtf8Charset()
        {
            // "café" in ISO-8859-1: decoding as UTF-8 would mangle the 0xE9 byte
            var body = new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE9 };
            var content = new ContentStream(new NetworkReader(new MemoryStream(body)), body.Length);
            var data = new RawPostData(MimeType.TextPlain);

            await data.SetAsync(new WebProgressTask(), content, "charset=iso-8859-1").ConfigureAwait(false);

            Assert.AreEqual("café", data.Encoding.GetString(data.Entry.Content!.Value.Span));
        }

        [TestMethod]
        public async Task TestSetAsyncResolvesAQuotedNonUtf8Charset()
        {
            // a quoted charset value (RFC 9110 §5.6.6) must be unwrapped, not fall back to UTF-8
            var body = new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE9 };
            var content = new ContentStream(new NetworkReader(new MemoryStream(body)), body.Length);
            var data = new RawPostData(MimeType.TextPlain);

            await data.SetAsync(new WebProgressTask(), content, "charset=\"iso-8859-1\"").ConfigureAwait(false);

            Assert.AreEqual("iso-8859-1", data.Encoding.WebName);
            Assert.AreEqual("café", data.Encoding.GetString(data.Entry.Content!.Value.Span));
        }

        [TestMethod]
        public async Task TestSetAsyncDefaultsToUtf8WhenNoCharsetIsDeclared()
        {
            var body = Encoding.UTF8.GetBytes("café");
            var content = new ContentStream(new NetworkReader(new MemoryStream(body)), body.Length);
            var data = new RawPostData(MimeType.TextPlain);

            await data.SetAsync(new WebProgressTask(), content, "").ConfigureAwait(false);

            Assert.AreEqual(Encoding.UTF8, data.Encoding);
        }
    }
}
