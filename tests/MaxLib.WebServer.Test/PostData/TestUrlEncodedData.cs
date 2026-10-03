using System;
using System.IO;
using System.Reflection;
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
    public class TestUrlEncodedData
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
            var body = Encoding.UTF8.GetBytes("firstKey=firstValue&secondKey=secondValueThatIsLonger");
            using var content = new ContentStream(new NetworkReader(new OneByteAtATimeStream(body)), body.Length);
            var data = new UrlEncodedData();

            await data.SetAsync(new WebProgressTask(), content, "").ConfigureAwait(false);

            Assert.AreEqual("firstValue", data.Parameter["firstKey"]);
            Assert.AreEqual("secondValueThatIsLonger", data.Parameter["secondKey"]);
        }

        [TestMethod]
        public void TestResolveEncodingResolvesAQuotedNonUtf8Charset()
        {
            // a quoted charset value must be unwrapped, not fall back to UTF-8; invoked via reflection
            // because ResolveEncoding is private
            var resolveEncoding = typeof(UrlEncodedData).GetMethod("ResolveEncoding", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("UrlEncodedData.ResolveEncoding method not found - test needs updating to match the current method name");

            var unquoted = (Encoding?)resolveEncoding.Invoke(null, ["charset=iso-8859-1"]);
            var quoted = (Encoding?)resolveEncoding.Invoke(null, ["charset=\"iso-8859-1\""]);

            Assert.AreEqual("iso-8859-1", unquoted?.WebName);
            Assert.AreEqual("iso-8859-1", quoted?.WebName);
        }

        // Forwards everything to the wrapped stream except writing, which always fails -
        // simulating a mid-write fault while spilling one oversized overflow value to disk.
        private sealed class ThrowsOnWriteStream(Stream inner) : Stream
        {
            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => inner.CanSeek;
            public override bool CanWrite => inner.CanWrite;
            public override long Length => inner.Length;
            public override long Position { get => inner.Position; set => inner.Position = value; }
            public override void Flush() => inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
            public override void SetLength(long value) => inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count)
                => throw new IOException("simulated write failure");
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
                CancellationToken cancellationToken = default)
                => throw new IOException("simulated write failure");
        }

        [TestMethod]
        public async Task TestAddTokenAsyncDeletesTheOrphanedTempFileWhenTheWriteThrowsPartway()
        {
            var originalLimit = UrlEncodedData.MaximumCacheSize;
            var originalMapper = MultipartFormData.StorageMapper;
            string? tempFilePath = null;
            try
            {
                // forces both the raw-body overflow spool and, since the single value alone
                // exceeds it too, the per-value temp-file branch in AddTokenAsync
                UrlEncodedData.MaximumCacheSize = 1;
                MultipartFormData.StorageMapper = (task, file) =>
                {
                    tempFilePath = ((FileStream)file).Name;
                    return new ThrowsOnWriteStream(file);
                };
                var body = Encoding.UTF8.GetBytes("key=thisvalueislongerthanonebyte");
                var content = new ContentStream(new NetworkReader(new MemoryStream(body)), body.Length);
                var data = new UrlEncodedData();

                await Assert.ThrowsExactlyAsync<IOException>(
                    () => data.SetAsync(new WebProgressTask(), content, "")
                ).ConfigureAwait(false);

                Assert.IsNotNull(tempFilePath);
                Assert.IsFalse(File.Exists(tempFilePath),
                    "the temp file must not be orphaned when the write into it fails partway through");
            }
            finally
            {
                UrlEncodedData.MaximumCacheSize = originalLimit;
                MultipartFormData.StorageMapper = originalMapper;
            }
        }
    }
}
