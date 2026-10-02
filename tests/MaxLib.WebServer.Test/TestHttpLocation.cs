using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestHttpLocation
    {
        [TestMethod]
        public void TestSetLocationParsesAnOrdinaryPathAndQuery()
        {
            var location = new HttpLocation("/foo/bar?x=1");

            Assert.AreEqual("/foo/bar", location.DocumentPath);
            CollectionAssert.AreEqual(new[] { "foo", "bar" }, location.DocumentPathTiles);
            Assert.AreEqual("1", location.GetParameter["x"]);
        }

        [TestMethod]
        public void TestSetLocationParsesMultipleQueryParameters()
        {
            // the query is split on '&' in code, not in the regex; every parameter must still come out in order
            var location = new HttpLocation("/foo?a=1&b=2&c=3");

            Assert.AreEqual("1", location.GetParameter["a"]);
            Assert.AreEqual("2", location.GetParameter["b"]);
            Assert.AreEqual("3", location.GetParameter["c"]);
        }

        [TestMethod]
        public void TestSetLocationFallsBackWhenQueryContainsALiteralDollarSign()
        {
            // a literal '$' in the query makes the regex fail, so SetLocation treats the whole raw string as the path
            var location = new HttpLocation("/foo?a=1$2");

            Assert.AreEqual("/foo?a=1$2", location.DocumentPath);
        }

        [TestMethod]
        public void TestSetLocationDoesNotHangOnAPathologicalQueryString()
        {
            // a query ending in many unmatched '$' must not cause catastrophic backtracking (ReDoS)
            var url = "/?" + new string('a', 10_000) + "$";

            var task = Task.Run(() => new HttpLocation(url));

            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(5)),
                "SetLocation took too long - looks like catastrophic backtracking regressed");
        }

        [TestMethod]
        public void TestSetLocationFallsBackToTheRawUrlWhenItDoesNotMatchTheRegex()
        {
            // RFC 7230 §5.3.4's request-target "*" (valid for e.g. "OPTIONS *") doesn't match
            // the leading-"/"-based regex at all. The fallback branch used to be immediately
            // overwritten by the unconditional assignments below it, silently turning this into
            // a request for "/" instead - any routing/access-control keyed on DocumentPath could
            // then unexpectedly match whatever is mounted at the root.
            var location = new HttpLocation("*");

            Assert.AreEqual("*", location.DocumentPath);
            CollectionAssert.AreEqual(new[] { "*" }, location.DocumentPathTiles);
            Assert.AreEqual("", location.CompleteGet);
            Assert.AreEqual(0, location.GetParameter.Count);
        }
    }
}
