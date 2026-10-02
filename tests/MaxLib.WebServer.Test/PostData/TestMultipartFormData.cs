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
    public class TestMultipartFormData
    {
        // Delivers allowedBytes successfully, then throws on every further read
        // (a client that stalls mid-upload).
        private sealed class ThrowsAfterNBytesStream(byte[] data, int allowedBytes) : MemoryStream(data)
        {
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (Position >= allowedBytes)
                    throw new IOException("simulated stalled upload");
                var maxLength = (int)Math.Min(buffer.Length, allowedBytes - Position);
                return base.ReadAsync(buffer[..maxLength], cancellationToken);
            }
        }

        [TestMethod]
        public async Task TestSetAsyncDeletesTheOrphanedTempFileWhenTheContentReadThrowsPartway()
        {
            var originalMapper = MultipartFormData.StorageMapper;
            string? tempFilePath = null;
            try
            {
                // AlwaysStoreFiles defaults to true, so a part with a filename always goes
                // through the temp-file branch regardless of MaximumCacheSize.
                var headerPart =
                    "-----1234\r\n" +
                    "Content-Type: text/plain\r\n" +
                    "Content-Disposition: form-data; name=\"f\"; filename=\"x\"\r\n" +
                    "\r\n";
                var bodyPart = "Hello World\r\n-----1234--\r\n";
                var content = headerPart + bodyPart;
                var contentBytes = Encoding.UTF8.GetBytes(content);
                var allowedBytes = Encoding.UTF8.GetByteCount(headerPart);

                MultipartFormData.StorageMapper = (task, file) =>
                {
                    tempFilePath = ((FileStream)file).Name;
                    return file;
                };
                var reader = new NetworkReader(new ThrowsAfterNBytesStream(contentBytes, allowedBytes));
                var contentStream = new ContentStream(reader, contentBytes.Length);
                var data = new MultipartFormData();

                await Assert.ThrowsExactlyAsync<IOException>(
                    () => data.SetAsync(new WebProgressTask(), contentStream, "boundary=---1234")
                ).ConfigureAwait(false);

                Assert.IsNotNull(tempFilePath);
                Assert.IsFalse(File.Exists(tempFilePath),
                    "the temp file must not be orphaned when reading the part's content fails partway through");
            }
            finally
            {
                MultipartFormData.StorageMapper = originalMapper;
            }
        }

        [TestMethod]
        public async Task TestSetAsyncRejectsAPartHeaderLineExceedingTheLengthLimit()
        {
            // a part header line without a CRLF must be rejected instead of buffering the whole remaining body
            var originalLimit = MultipartFormData.MaxPartHeaderLineLength;
            try
            {
                MultipartFormData.MaxPartHeaderLineLength = 16;
                var content =
                    "-----1234\r\n" +
                    "X-Very-Long-Header-Name-That-Is-Way-Too-Long: value\r\n" +
                    "\r\n" +
                    "content\r\n" +
                    "-----1234--\r\n";
                var contentBytes = Encoding.UTF8.GetBytes(content);
                var contentStream = new ContentStream(new NetworkReader(new MemoryStream(contentBytes)), contentBytes.Length);
                var data = new MultipartFormData();
                var task = new WebProgressTask();

                await data.SetAsync(task, contentStream, "boundary=---1234").ConfigureAwait(false);

                Assert.AreEqual(HttpStateCode.RequestHeaderFieldsTooLarge, task.Response.StatusCode);
            }
            finally
            {
                MultipartFormData.MaxPartHeaderLineLength = originalLimit;
            }
        }
    }
}
