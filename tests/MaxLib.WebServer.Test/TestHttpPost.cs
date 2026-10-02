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
    }
}
