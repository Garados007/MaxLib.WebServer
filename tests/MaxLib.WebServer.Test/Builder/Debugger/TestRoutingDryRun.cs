using System.Linq;
using MaxLib.WebServer.Builder.Debugger;
using MaxLib.WebServer.Builder.Tools;
using MaxLib.WebServer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder.Debugger
{
    [TestClass]
    public class TestRoutingDryRun
    {
        [TestMethod]
        public void TestHigherPriorityWinsAndLowerIsNotReached()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            // added in reverse-priority order to make sure the dry run relies on the group's
            // own priority ordering, not insertion order.
            group.Add(Generator.GenerateMethod(typeof(DupLowFixture).GetMethod("M")!)!);
            group.Add(Generator.GenerateMethod(typeof(DupHighFixture).GetMethod("M")!)!);

            var report = RoutingDryRun.Run(group, "GET", "/dup");

            Assert.AreEqual(2, report.Services.Count);
            Assert.AreEqual(RoutingOutcome.Accepted, report.Services[0].Outcome);
            Assert.AreEqual(RoutingOutcome.NotReached, report.Services[1].Outcome);
            Assert.AreEqual("DupHighFixture.M()", report.MatchedLabel);
        }

        [TestMethod]
        public void TestMethodMismatchIsExplained()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(Generator.GenerateMethod(typeof(GetOnlyFixture).GetMethod("M")!)!);

            var report = RoutingDryRun.Run(group, "POST", "/only-get");

            Assert.AreEqual(RoutingOutcome.Rejected, report.Services[0].Outcome);
            Assert.IsTrue(report.Services[0].Reasons.Any(r => r.Contains("GET") && r.Contains("POST")));
            Assert.IsNull(report.MatchedLabel);
        }

        [TestMethod]
        public void TestPathMismatchIsExplained()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(Generator.GenerateMethod(typeof(GetOnlyFixture).GetMethod("M")!)!);

            var report = RoutingDryRun.Run(group, "GET", "/does-not-exist");

            Assert.AreEqual(RoutingOutcome.Rejected, report.Services[0].Outcome);
            Assert.IsTrue(report.Services[0].Reasons.Count > 0);
        }

        [TestMethod]
        public void TestUnresolvableParameterIsExplained()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(Generator.GenerateMethod(typeof(MethodFixtures).GetMethod("WorkingParamMethod")!)!);

            // nothing provides the "x" variable the [Var] parameter needs.
            var report = RoutingDryRun.Run(group, "GET", "/");

            Assert.AreEqual(RoutingOutcome.Rejected, report.Services[0].Outcome);
            Assert.IsTrue(report.Services[0].Reasons.Any(r => r.Contains("'x'")));
        }

        [TestMethod]
        public void TestServiceGroupTraversal()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(Generator.GenerateClass(typeof(GroupedFixture))!);

            var accepted = RoutingDryRun.Run(group, "GET", "/grouped/only");
            Assert.AreEqual(RoutingOutcome.Accepted, accepted.Services[0].Outcome);
            Assert.AreEqual(1, accepted.Services[0].Children.Count);
            Assert.AreEqual(RoutingOutcome.Accepted, accepted.Services[0].Children[0].Outcome);
            Assert.AreEqual("GroupedFixture.M()", accepted.MatchedLabel);

            var rejected = RoutingDryRun.Run(group, "GET", "/elsewhere");
            Assert.AreEqual(RoutingOutcome.Rejected, rejected.Services[0].Outcome);
            // the class-level rule already failed, so the nested method was never evaluated.
            Assert.AreEqual(0, rejected.Services[0].Children.Count);
            Assert.IsTrue(rejected.Services[0].Reasons.Count > 0);
        }

        [TestMethod]
        public void TestGenericFallbackService()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(Generator.GenerateMethod(typeof(GetOnlyFixture).GetMethod("M")!)!);
            group.Add(new Http404Service());

            var report = RoutingDryRun.Run(group, "POST", "/only-get");

            Assert.AreEqual(RoutingOutcome.Rejected, report.Services[0].Outcome);
            Assert.AreEqual(RoutingOutcome.Accepted, report.Services[1].Outcome);
            Assert.AreEqual(nameof(Http404Service), report.MatchedLabel);
        }

        [TestMethod]
        public void TestGenericFallbackServiceRejection()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(new AlwaysRejectService());

            var report = RoutingDryRun.Run(group, "GET", "/");

            Assert.AreEqual(RoutingOutcome.Rejected, report.Services[0].Outcome);
            Assert.IsTrue(report.Services[0].Reasons.Contains("CanWorkWith() returned false"));
        }

        [TestMethod]
        public void TestHeadersArePassedToTheSyntheticRequest()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(new Http404Service());

            var report = RoutingDryRun.Run(group, "GET", "/", new System.Collections.Generic.Dictionary<string, string>
            {
                ["X-Test"] = "value",
            });

            Assert.AreEqual(RoutingOutcome.Accepted, report.Services[0].Outcome);
        }

        [TestMethod]
        public void TestSecondMatchingChildInServiceGroupIsNotReached()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(Generator.GenerateClass(typeof(InnerDupFixture))!);

            var report = RoutingDryRun.Run(group, "GET", "/inner-dup/x");

            Assert.AreEqual(RoutingOutcome.Accepted, report.Services[0].Outcome);
            Assert.AreEqual(2, report.Services[0].Children.Count);
            Assert.AreEqual(RoutingOutcome.Accepted, report.Services[0].Children[0].Outcome);
            Assert.AreEqual(RoutingOutcome.NotReached, report.Services[0].Children[1].Outcome);
            Assert.AreEqual("InnerDupFixture.A()", report.MatchedLabel);
        }

        [TestMethod]
        public void TestNoChildMatchesInsideServiceGroup()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(Generator.GenerateClass(typeof(NoMatchingChildFixture))!);

            // matches the group's class-level prefix, but no child accepts GET (only POST is
            // registered for the sub-path).
            var report = RoutingDryRun.Run(group, "GET", "/none-matches/only");

            Assert.AreEqual(RoutingOutcome.Rejected, report.Services[0].Outcome);
            Assert.AreEqual(1, report.Services[0].Children.Count);
            Assert.AreEqual(RoutingOutcome.Rejected, report.Services[0].Children[0].Outcome);
            Assert.IsTrue(report.Services[0].Reasons.Contains("none of the contained services matched the request"));
        }

        [TestMethod]
        public void TestUnresolvableCoreParameterIsExplainedByReflectedName()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(Generator.GenerateMethod(typeof(CoreParamFixture).GetMethod("M")!)!);

            // no Connection is set on the synthetic task, so the IPEndPoint core parameter can
            // never resolve.
            var report = RoutingDryRun.Run(group, "GET", "/core-param");

            Assert.AreEqual(RoutingOutcome.Rejected, report.Services[0].Outcome);
            Assert.IsTrue(report.Services[0].Reasons.Any(r => r.Contains("'endpoint'")));
        }

        [TestMethod]
        public void TestParameterConversionFailureIsExplainedInsteadOfThrowing()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(Generator.GenerateMethod(typeof(ConversionFailureFixture).GetMethod("M")!)!);

            // a non-numeric value for an int parameter must show up as a rejection reason, not as an uncaught HttpException
            var report = RoutingDryRun.Run(group, "GET", "/conv-fail?id=notanumber");

            Assert.AreEqual(RoutingOutcome.Rejected, report.Services[0].Outcome);
            Assert.IsTrue(report.Services[0].Reasons.Any(r => r.Contains("failed to convert")));
        }

        [TestMethod]
        public void TestNonExplainableRuleFallsBackToGenericReason()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(Generator.GenerateMethod(typeof(NonExplainableRuleFixture).GetMethod("M")!)!);

            var report = RoutingDryRun.Run(group, "GET", "/non-explainable");

            Assert.AreEqual(RoutingOutcome.Rejected, report.Services[0].Outcome);
            Assert.IsTrue(report.Services[0].Reasons.Any(r => r.Contains("AlwaysFail") && r.Contains("rejected the request")));
        }
    }
}
