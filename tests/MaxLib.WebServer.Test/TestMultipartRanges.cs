using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestMultipartRanges
    {
        [TestMethod]
        public async Task TestSingleRangeIncludesTheFinalRequestedByte()
        {
            // To is inclusive: "bytes=0-4" must deliver 5 bytes, so the count is To - From + 1
            var request = new HttpRequestHeader();
            request.HeaderParameter["Range"] = "bytes=0-4";
            var response = new HttpResponseHeader();
            using var baseStream = new MemoryStream(Encoding.UTF8.GetBytes("0123456789"));
            using var ranges = new MultipartRanges(baseStream, request, response, null);

            using var destination = new MemoryStream();
            await ranges.WriteStream(destination).ConfigureAwait(false);

            Assert.AreEqual(HttpStateCode.PartialContent, response.StatusCode);
            Assert.AreEqual("bytes 0-4/10", response.HeaderParameter["Content-Range"]);
            Assert.AreEqual("01234", Encoding.UTF8.GetString(destination.ToArray()));
        }

        [TestMethod]
        public async Task TestOutOfOrderRangesStillMergeWithoutDroppingBytes()
        {
            // FormatRanges sorts by From before merging; this dataset fails with a wrong comparator
            // (List.Sort with an invalid comparator is unspecified, so smaller inputs may not catch it)
            var originalJoinGap = MultipartRanges.JoinGap;
            try
            {
                MultipartRanges.JoinGap = 0;
                var request = new HttpRequestHeader();
                // ranges 90-125, 125-151, 147-192 and 173-211 all mutually overlap/touch and
                // must merge into one 90-211 span regardless of the order they were requested in
                request.HeaderParameter["Range"] = "bytes=44-79,90-125,147-192,125-151,173-211";
                var response = new HttpResponseHeader();
                var sourceBytes = new byte[250];
                for (var i = 0; i < sourceBytes.Length; ++i)
                    sourceBytes[i] = (byte)i;
                using var baseStream = new MemoryStream(sourceBytes);
                using var ranges = new MultipartRanges(baseStream, request, response, null);

                using var destination = new MemoryStream();
                await ranges.WriteStream(destination).ConfigureAwait(false);

                // bytes 152-172 sit inside the merged 90-211 span and must appear as one
                // uninterrupted run in the output, wherever the multipart formatting places it
                var expectedRun = sourceBytes[152..173];
                Assert.IsTrue(ContainsSubsequence(destination.ToArray(), expectedRun),
                    "bytes 152-172 are missing from the response - the out-of-order ranges failed to merge correctly");
            }
            finally
            {
                MultipartRanges.JoinGap = originalJoinGap;
            }
        }

        [TestMethod]
        public void TestReversedRangeIsRejectedAsNotSatisfiable()
        {
            // both bounds of "bytes=5-2" are in range but From > To; it must be rejected
            // instead of the negative byte count throwing
            var request = new HttpRequestHeader();
            request.HeaderParameter["Range"] = "bytes=5-2";
            var response = new HttpResponseHeader();
            using var baseStream = new MemoryStream(Encoding.UTF8.GetBytes("0123456789"));

            using var ranges = new MultipartRanges(baseStream, request, response, null);

            Assert.AreEqual(HttpStateCode.RequestedRangeNotSatisfiable, response.StatusCode);
        }

        private static bool ContainsSubsequence(byte[] haystack, byte[] needle)
        {
            for (var i = 0; i <= haystack.Length - needle.Length; ++i)
            {
                var match = true;
                for (var j = 0; j < needle.Length; ++j)
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                if (match)
                    return true;
            }
            return false;
        }
    }
}
