using System.Collections.Generic;
using System.IO;
using System.Text;
using MaxLib.WebServer.Builder;
using MaxLib.WebServer.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder
{
    [TestClass]
    public class TestTextPostAttribute
    {
        [TestMethod]
        public void TestGetValueReadsAnInMemoryRawPostDataAsText()
        {
            var task = new WebProgressTask();
            var body = Encoding.UTF8.GetBytes("hello raw body");
            task.Request.Post.SetPost(
                task,
                new ContentStream(new NetworkReader(new MemoryStream(body)), body.Length),
                MimeType.ApplicationOctetStream
            );

            var result = new TextPostAttribute().GetValue(task, "", new Dictionary<string, object?>());

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual("hello raw body", result.Value);
        }

        [TestMethod]
        public void TestGetValueReadsAFileBackedRawPostDataAsText()
        {
            var originalLimit = Post.RawPostData.MaximumCacheSize;
            try
            {
                Post.RawPostData.MaximumCacheSize = 1;
                var task = new WebProgressTask();
                var body = Encoding.UTF8.GetBytes("a body larger than the 1-byte limit");
                task.Request.Post.SetPost(
                    task,
                    new ContentStream(new NetworkReader(new MemoryStream(body)), body.Length),
                    MimeType.ApplicationOctetStream
                );

                var result = new TextPostAttribute().GetValue(task, "", new Dictionary<string, object?>());

                Assert.IsTrue(result.HasValue);
                Assert.AreEqual("a body larger than the 1-byte limit", result.Value);
            }
            finally
            {
                Post.RawPostData.MaximumCacheSize = originalLimit;
            }
        }

        [TestMethod]
        public void TestGetValueReturnsNoValueForAKnownPostDataType()
        {
            var task = new WebProgressTask();
            var body = Encoding.UTF8.GetBytes("foo=bar");
            task.Request.Post.SetPost(
                task,
                new ContentStream(new NetworkReader(new MemoryStream(body)), body.Length),
                MimeType.ApplicationXWwwFromUrlencoded
            );

            var result = new TextPostAttribute().GetValue(task, "", new Dictionary<string, object?>());

            Assert.IsFalse(result.HasValue);
        }
    }
}
