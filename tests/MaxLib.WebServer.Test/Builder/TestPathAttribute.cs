using System.Collections.Generic;
using MaxLib.WebServer.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder
{
    [TestClass]
    public class TestPathAttribute
    {
        [TestMethod]
        public void TestPrefixModeRejectsAUrlShorterThanTheTemplate()
        {
            // a URL shorter than the template must never match, even in Prefix mode
            var attr = new PathAttribute("/admin/secret/{token}") { Prefix = true };
            var task = new WebProgressTask();
            task.Request.Url = "/admin/secret";
            var vars = new Dictionary<string, object?>();

            var matched = attr.CanWorkWith(task, vars);

            Assert.IsFalse(matched, "a URL missing the template's final {token} segment must not match");
            Assert.AreEqual(0, vars.Count);
        }

        [TestMethod]
        public void TestPrefixModeRejectsTheRootUrlAgainstAMultiSegmentTemplate()
        {
            var attr = new PathAttribute("/admin/secret/{token}") { Prefix = true };
            var task = new WebProgressTask();
            task.Request.Url = "/";
            var vars = new Dictionary<string, object?>();

            Assert.IsFalse(attr.CanWorkWith(task, vars));
        }

        [TestMethod]
        public void TestPrefixModeStillMatchesAUrlAtLeastAsLongAsTheTemplate()
        {
            var attr = new PathAttribute("/admin/secret/{token}") { Prefix = true };
            var task = new WebProgressTask();
            task.Request.Url = "/admin/secret/abc123/extra";
            var vars = new Dictionary<string, object?>();

            var matched = attr.CanWorkWith(task, vars);

            Assert.IsTrue(matched);
            Assert.AreEqual("abc123", vars["token"]);
        }

        [TestMethod]
        public void TestNonPrefixModeStillRequiresAnExactLengthMatch()
        {
            var attr = new PathAttribute("/admin/secret/{token}") { Prefix = false };
            var task = new WebProgressTask();
            task.Request.Url = "/admin/secret";
            var vars = new Dictionary<string, object?>();

            Assert.IsFalse(attr.CanWorkWith(task, vars));
        }

        [TestMethod]
        public void TestExplainableRulePrefixModeAlsoRejectsAUrlShorterThanTheTemplate()
        {
            // the IExplainableRule copy used by RoutingDryRun must agree with CanWorkWith above
            var attr = new PathAttribute("/admin/secret/{token}") { Prefix = true };
            var task = new WebProgressTask();
            task.Request.Url = "/admin/secret";
            var vars = new Dictionary<string, object?>();

            var matched = ((MaxLib.WebServer.Builder.Debugger.IExplainableRule)attr).CanWorkWith(task, vars, out var reason);

            Assert.IsFalse(matched);
            Assert.IsNotNull(reason);
            Assert.AreEqual(0, vars.Count);
        }
    }
}
