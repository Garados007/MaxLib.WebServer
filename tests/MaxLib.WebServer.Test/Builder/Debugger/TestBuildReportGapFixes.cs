using System.Linq;
using System.Reflection;
using MaxLib.WebServer.Builder;
using MaxLib.WebServer.Builder.Debugger;
using MaxLib.WebServer.Builder.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder.Debugger
{
    /// <summary>
    /// Verifies that an <see cref="IgnoreAttribute" />-only skip, a constructor exception, and a
    /// non-<see cref="Service" /> type are each attributed correctly in <see cref="BuildReport"
    /// />, and that a constructor exception in one type does not abort the rest of the scan.
    /// </summary>
    [TestClass]
    public class TestBuildReportGapFixes
    {
        [TestMethod]
        public void TestIgnoredSkipIsAttributedEvenWhenNotLogged()
        {
            // the default GeneratorLogFlag configuration doesn't include the "ignored by
            // attribute" flags (they are considered intentional) - the report must still surface
            // the reason regardless of the log-gating configuration.
            Assert.AreNotEqual(
                GeneratorLogFlag.TypeIgnoredByAttribute,
                Generator.LogBuildWarnings & GeneratorLogFlag.TypeIgnoredByAttribute
            );

            var group = Generator.GenerateClass(typeof(IgnoredTypeFixture), out var typeReport);
            Assert.IsNull(group);
            Assert.AreEqual(BuildReasonCode.TypeIgnoredByAttribute, typeReport.Reason);

            var method = Generator.GenerateMethod(
                typeof(MethodFixtures).GetMethod("IgnoredMethod")!, out var methodReport
            );
            Assert.IsNull(method);
            Assert.AreEqual(BuildReasonCode.MethodIgnoredByAttribute, methodReport.Reason);
        }

        [TestMethod]
        public void TestConstructorExceptionIsCaughtAndAttributed()
        {
            var group = Generator.GenerateClass(typeof(ThrowingConstructorFixture), out var report);

            // the type itself is still a valid (empty) service group - only the one broken
            // method failed, it didn't abort the whole type.
            Assert.IsNotNull(group);
            Assert.AreEqual(0, group!.Count);
            Assert.AreEqual(BuildReportStatus.Accepted, report.Status);

            var methodReport = report.Methods.Single(m => m.Name == "M");
            Assert.AreEqual(BuildReportStatus.Failed, methodReport.Status);
            Assert.AreEqual(BuildReasonCode.MethodConstructorThrew, methodReport.Reason);
            Assert.AreEqual("boom", methodReport.ExceptionMessage);
            Assert.IsNotNull(methodReport.Exception);
        }

        [TestMethod]
        public void TestConstructorExceptionDoesNotAbortAssemblyScan()
        {
            var result = Service.Build(Assembly.GetExecutingAssembly(), out var report);
            Assert.IsNotNull(result);

            var throwingNode = report.Roots.Single(r => r.TypeFullName == typeof(ThrowingConstructorFixture).FullName);
            Assert.AreEqual(BuildReportStatus.Accepted, throwingNode.Status);
            var throwingMethod = throwingNode.Methods.Single(m => m.Name == "M");
            Assert.AreEqual(BuildReasonCode.MethodConstructorThrew, throwingMethod.Reason);

            // a healthy, unrelated service in the same assembly still builds successfully.
            var healthyNode = report.Roots.Single(r => r.TypeFullName == typeof(HealthyFixture).FullName);
            Assert.AreEqual(BuildReportStatus.Accepted, healthyNode.Status);
            Assert.IsTrue(healthyNode.Methods.Any(m => m.Name == "M" && m.Status == BuildReportStatus.Accepted));
        }

        [TestMethod]
        public void TestNonServiceTypeIsAttributedForSingleTypeBuild()
        {
            var result = Service.Build(typeof(string), out var report);
            Assert.IsNull(result);
            Assert.AreEqual(1, report.Roots.Count);
            Assert.AreEqual(BuildReportStatus.Failed, report.Roots[0].Status);
            Assert.AreEqual(BuildReasonCode.TypeNotService, report.Roots[0].Reason);
        }

        [TestMethod]
        public void TestNonServiceTypesAreOnlyCountedForBulkScans()
        {
            Service.Build(Assembly.GetExecutingAssembly(), out var report);

            // the test assembly contains many non-Service classes (test classes, fixtures, ...)
            // - they must not flood Roots with a TypeNotService node each.
            Assert.IsTrue(report.SkippedNonServiceTypeCount > 0);
            Assert.IsFalse(report.Roots.Any(r => r.Reason == BuildReasonCode.TypeNotService));
        }
    }
}
