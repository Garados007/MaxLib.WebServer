using System;
using System.IO;
using System.Threading.Tasks;
using MaxLib.WebServer.IO;
using MaxLib.WebServer.Post;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestHttpPost
    {
        private sealed class ThrowingPostData : IPostData
        {
            public bool Disposed { get; private set; }

            public string MimeType => "test/throwing";

            public Task SetAsync(WebProgressTask task, ContentStream content, string options)
                => throw new InvalidOperationException("boom");

            public void Dispose() => Disposed = true;
        }

        [TestMethod]
        public async Task TestDisposesThePartiallyBuiltPostDataWhenSetAsyncThrows()
        {
            // SetAsync may have created temp files before failing; they must be cleaned up
            // even though the IPostData is otherwise unreachable.
            var postData = new ThrowingPostData();
            HttpPost.DataHandler["test/throwing"] = () => postData;
            try
            {
                var post = new HttpPost();
                var content = new ContentStream(new NetworkReader(new MemoryStream([])), 0);
                post.SetPost(new WebProgressTask(), content, "test/throwing");

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    async () => await post.DataAsync!.ConfigureAwait(false));

                Assert.IsTrue(postData.Disposed);
            }
            finally
            {
                HttpPost.DataHandler.Remove("test/throwing");
            }
        }

        [TestMethod]
        public void TestSetPostRecognizesAContentTypeRegardlessOfCase()
        {
            // a non-lowercase Content-Type must still resolve to the registered IPostData type
            var post = new HttpPost();
            var content = new ContentStream(new NetworkReader(new MemoryStream([])), 0);

            post.SetPost(new WebProgressTask(), content, "Application/X-WWW-Form-Urlencoded");

            Assert.IsInstanceOfType<Post.UrlEncodedData>(post.Data);
        }

        [TestMethod]
        public void TestSetPostIgnoresWhitespaceAroundTheParameterSeparator()
        {
            // RFC 9110 allows optional whitespace before the `;` - it must not end up in the media type
            var post = new HttpPost();
            var content = new ContentStream(new NetworkReader(new MemoryStream([])), 0);

            post.SetPost(new WebProgressTask(), content, "application/x-www-form-urlencoded ; charset=utf-8");

            Assert.AreEqual("application/x-www-form-urlencoded", post.MimeType);
            Assert.IsInstanceOfType<Post.UrlEncodedData>(post.Data);
        }

        [TestMethod]
        public void TestSetPostParsesMultipartWithWhitespaceBeforeTheBoundaryParameter()
        {
            var body = "--x\r\n"
                + "Content-Disposition: form-data; name=\"field\"\r\n"
                + "\r\n"
                + "value\r\n"
                + "--x--\r\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            var post = new HttpPost();
            var content = new ContentStream(new NetworkReader(new MemoryStream(bytes)), bytes.Length);

            post.SetPost(new WebProgressTask(), content, "multipart/form-data ; boundary=x");

            var data = post.Data as Post.MultipartFormData;
            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Entries.Count);
        }
    }
}
