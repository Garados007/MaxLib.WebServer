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
