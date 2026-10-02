using System.Collections.Generic;
using System.IO;
using System.Text;
using MaxLib.WebServer.Builder;
using MaxLib.WebServer.IO;
using MaxLib.WebServer.Post;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder
{
    [TestClass]
    public class TestUrlEncodedPostAttribute
    {
        [TestMethod]
        public void TestGetValueReadsAnInMemoryParameterBelowTheOverflowThreshold()
        {
            var task = new WebProgressTask();
            var body = Encoding.UTF8.GetBytes("name=world");
            task.Request.Post.SetPost(
                task,
                new ContentStream(new NetworkReader(new MemoryStream(body)), body.Length),
                MimeType.ApplicationXWwwFromUrlencoded
            );

            var result = new UrlEncodedPostAttribute("name").GetValue(task, "", new Dictionary<string, object?>());

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual("world", result.Value);
        }

        [TestMethod]
        public void TestGetValueReadsAnInMemoryOverflowEntry()
        {
            // once the body exceeds MaximumCacheSize, fields are in Overflow rather than Parameter
            var originalLimit = UrlEncodedData.MaximumCacheSize;
            try
            {
                UrlEncodedData.MaximumCacheSize = 1;
                var task = new WebProgressTask();
                var body = Encoding.UTF8.GetBytes("name=x");
                task.Request.Post.SetPost(
                    task,
                    new ContentStream(new NetworkReader(new MemoryStream(body)), body.Length),
                    MimeType.ApplicationXWwwFromUrlencoded
                );

                var result = new UrlEncodedPostAttribute("name").GetValue(task, "", new Dictionary<string, object?>());

                Assert.IsTrue(result.HasValue,
                    "the field must still resolve via Overflow once the body exceeds MaximumCacheSize");
                Assert.AreEqual("x", result.Value);
            }
            finally
            {
                UrlEncodedData.MaximumCacheSize = originalLimit;
            }
        }

        [TestMethod]
        public void TestGetValueReadsAFileBackedOverflowEntry()
        {
            // a value that itself exceeds MaximumCacheSize is spilled to a temp file in Overflow; GetValue must read it too
            var originalLimit = UrlEncodedData.MaximumCacheSize;
            try
            {
                UrlEncodedData.MaximumCacheSize = 1;
                var task = new WebProgressTask();
                var body = Encoding.UTF8.GetBytes("name=a-value-longer-than-the-limit");
                task.Request.Post.SetPost(
                    task,
                    new ContentStream(new NetworkReader(new MemoryStream(body)), body.Length),
                    MimeType.ApplicationXWwwFromUrlencoded
                );

                var result = new UrlEncodedPostAttribute("name").GetValue(task, "", new Dictionary<string, object?>());

                Assert.IsTrue(result.HasValue);
                Assert.AreEqual("a-value-longer-than-the-limit", result.Value);
            }
            finally
            {
                UrlEncodedData.MaximumCacheSize = originalLimit;
            }
        }

        [TestMethod]
        public void TestGetValueReturnsNoValueForAnUnknownFieldPastTheOverflowThreshold()
        {
            var originalLimit = UrlEncodedData.MaximumCacheSize;
            try
            {
                UrlEncodedData.MaximumCacheSize = 1;
                var task = new WebProgressTask();
                var body = Encoding.UTF8.GetBytes("name=x");
                task.Request.Post.SetPost(
                    task,
                    new ContentStream(new NetworkReader(new MemoryStream(body)), body.Length),
                    MimeType.ApplicationXWwwFromUrlencoded
                );

                var result = new UrlEncodedPostAttribute("missing").GetValue(task, "", new Dictionary<string, object?>());

                Assert.IsFalse(result.HasValue);
            }
            finally
            {
                UrlEncodedData.MaximumCacheSize = originalLimit;
            }
        }
    }
}
