using System.Linq;
using System.Reflection;
using MaxLib.WebServer.Builder;
using MaxLib.WebServer.Builder.Debugger;
using MaxLib.WebServer.Builder.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder.Debugger
{
    [TestClass]
    public class TestBuildReport
    {
        [TestMethod]
        public void TestTypeIgnoredByAttribute()
        {
            var group = Generator.GenerateClass(typeof(IgnoredTypeFixture), out var report);
            Assert.IsNull(group);
            Assert.AreEqual(BuildReportStatus.Ignored, report.Status);
            Assert.AreEqual(BuildReasonCode.TypeIgnoredByAttribute, report.Reason);

            // the plain (no-report) overload keeps behaving identically.
            Assert.IsNull(Generator.GenerateClass(typeof(IgnoredTypeFixture)));
        }

        [TestMethod]
        public void TestTypeAbstract()
        {
            var group = Generator.GenerateClass(typeof(AbstractTypeFixture), out var report);
            Assert.IsNull(group);
            Assert.AreEqual(BuildReportStatus.Ignored, report.Status);
            Assert.AreEqual(BuildReasonCode.TypeAbstract, report.Reason);
        }

        [TestMethod]
        public void TestTypeGeneric()
        {
            var group = Generator.GenerateClass(typeof(GenericTypeFixture<int>), out var report);
            Assert.IsNull(group);
            Assert.AreEqual(BuildReportStatus.Ignored, report.Status);
            Assert.AreEqual(BuildReasonCode.TypeGeneric, report.Reason);
        }

        [TestMethod]
        public void TestTypeNoConstructor()
        {
            var group = Generator.GenerateClass(typeof(NoConstructorTypeFixture), out var report);
            Assert.IsNull(group);
            Assert.AreEqual(BuildReportStatus.Ignored, report.Status);
            Assert.AreEqual(BuildReasonCode.TypeNoConstructor, report.Reason);
        }

        [TestMethod]
        public void TestTypeAccepted()
        {
            var group = Generator.GenerateClass(typeof(HealthyFixture), out var report);
            Assert.IsNotNull(group);
            Assert.AreEqual(BuildReportStatus.Accepted, report.Status);
            Assert.AreEqual(BuildReasonCode.Accepted, report.Reason);
            Assert.AreEqual(1, report.Methods.Count(m => m.Name == "M"));
            var methodReport = report.Methods.Single(m => m.Name == "M");
            Assert.AreEqual(BuildReportStatus.Accepted, methodReport.Status);
            Assert.IsNotNull(methodReport.Result);
        }

        [TestMethod]
        public void TestMethodIgnoredByAttribute()
        {
            var method = Generator.GenerateMethod(
                typeof(MethodFixtures).GetMethod("IgnoredMethod")!, out var report
            );
            Assert.IsNull(method);
            Assert.AreEqual(BuildReportStatus.Ignored, report.Status);
            Assert.AreEqual(BuildReasonCode.MethodIgnoredByAttribute, report.Reason);
        }

        [TestMethod]
        public void TestMethodNotPublic()
        {
            var methodInfo = typeof(MethodFixtures).GetMethod("NotPublicMethod",
                BindingFlags.NonPublic | BindingFlags.Instance
            )!;
            var method = Generator.GenerateMethod(methodInfo, out var report);
            Assert.IsNull(method);
            Assert.AreEqual(BuildReportStatus.Ignored, report.Status);
            Assert.AreEqual(BuildReasonCode.MethodNotPublic, report.Reason);
        }

        [TestMethod]
        public void TestMethodGeneric()
        {
            var method = Generator.GenerateMethod(
                typeof(MethodFixtures).GetMethod("GenericMethod")!, out var report
            );
            Assert.IsNull(method);
            Assert.AreEqual(BuildReportStatus.Ignored, report.Status);
            Assert.AreEqual(BuildReasonCode.MethodGeneric, report.Reason);
        }

        [TestMethod]
        public void TestMethodAbstract()
        {
            var method = Generator.GenerateMethod(
                typeof(AbstractMethodFixture).GetMethod("M")!, out var report
            );
            Assert.IsNull(method);
            Assert.AreEqual(BuildReportStatus.Ignored, report.Status);
            Assert.AreEqual(BuildReasonCode.MethodAbstract, report.Reason);
        }

        [TestMethod]
        public void TestMethodDeclaredInObject()
        {
            var method = Generator.GenerateMethod(
                typeof(HealthyFixture).GetMethod("ToString")!, out var report
            );
            Assert.IsNull(method);
            Assert.AreEqual(BuildReportStatus.Ignored, report.Status);
            Assert.AreEqual(BuildReasonCode.MethodDeclaredInObject, report.Reason);
        }

        [TestMethod]
        public void TestParamNoConverterFound()
        {
            var method = Generator.GenerateMethod(
                typeof(MethodFixtures).GetMethod("ParamNoConverterMethod")!, out var report
            );
            Assert.IsNull(method);
            Assert.AreEqual(BuildReportStatus.Failed, report.Status);
            Assert.AreEqual(BuildReasonCode.ParamNoConverterFound, report.Reason);
            Assert.AreEqual(1, report.Parameters.Count);
            Assert.AreEqual(BuildReasonCode.ParamNoConverterFound, report.Parameters[0].Reason);
        }

        [TestMethod]
        public void TestParamNoCoreConverterFound()
        {
            var method = Generator.GenerateMethod(
                typeof(MethodFixtures).GetMethod("ParamNoCoreConverterMethod")!, out var report
            );
            Assert.IsNull(method);
            Assert.AreEqual(BuildReportStatus.Failed, report.Status);
            Assert.AreEqual(BuildReasonCode.ParamNoCoreConverterFound, report.Reason);
        }

        [TestMethod]
        public void TestParamMissingConverterInstance()
        {
            var method = Generator.GenerateMethod(
                typeof(MethodFixtures).GetMethod("ParamMissingConverterInstanceMethod")!, out var report
            );
            Assert.IsNull(method);
            Assert.AreEqual(BuildReportStatus.Failed, report.Status);
            Assert.AreEqual(BuildReasonCode.ParamMissingConverterInstance, report.Reason);
        }

        [TestMethod]
        public void TestResultInvalidConverterType()
        {
            var method = Generator.GenerateMethod(
                typeof(MethodFixtures).GetMethod("ResultInvalidConverterTypeMethod")!, out var report
            );
            Assert.IsNull(method);
            Assert.AreEqual(BuildReportStatus.Failed, report.Status);
            Assert.AreEqual(BuildReasonCode.ResultInvalidConverterType, report.Reason);
            Assert.IsNotNull(report.Result);
            Assert.AreEqual(BuildReasonCode.ResultInvalidConverterType, report.Result!.Reason);
        }

        [TestMethod]
        public void TestResultCannotCreateConverterInstance()
        {
            var method = Generator.GenerateMethod(
                typeof(MethodFixtures).GetMethod("ResultCannotCreateConverterInstanceMethod")!, out var report
            );
            Assert.IsNull(method);
            Assert.AreEqual(BuildReportStatus.Failed, report.Status);
            Assert.AreEqual(BuildReasonCode.ResultCannotCreateConverterInstance, report.Reason);
        }

        [TestMethod]
        public void TestResultNoConverter()
        {
            var method = Generator.GenerateMethod(
                typeof(MethodFixtures).GetMethod("ResultNoConverterMethod")!, out var report
            );
            Assert.IsNull(method);
            Assert.AreEqual(BuildReportStatus.Failed, report.Status);
            Assert.AreEqual(BuildReasonCode.ResultNoConverter, report.Reason);
        }

        [TestMethod]
        public void TestGenerateParameterDirectOutOverload()
        {
            var method = typeof(MethodFixtures).GetMethod("WorkingParamMethod")!;
            var parameter = Generator.GenerateParameter(method, method.GetParameters()[0], out var report);
            Assert.IsNotNull(parameter);
            Assert.AreEqual(BuildReportStatus.Accepted, report.Status);
            Assert.AreEqual("x", report.Name);
        }

        [TestMethod]
        public void TestGenerateResultDirectOutOverload()
        {
            var method = typeof(HealthyFixture).GetMethod("M")!;
            var result = Generator.GenerateResult(method, out var report);
            Assert.IsNotNull(result);
            Assert.AreEqual(BuildReportStatus.Accepted, report.Status);
        }

        [TestMethod]
        public void TestNestedTypeIsReported()
        {
            var group = Generator.GenerateClass(typeof(NestedTypeFixture), out var report);
            Assert.IsNotNull(group);
            var nested = report.NestedTypes.Single(n => n.Name == nameof(NestedTypeFixture.Inner));
            Assert.AreEqual(BuildReportStatus.Accepted, nested.Status);
            Assert.IsTrue(nested.Methods.Any(m => m.Name == "M" && m.Status == BuildReportStatus.Accepted));
        }

        [TestMethod]
        public void TestBuildReportWalkAndCount()
        {
            // HealthyFixture.M() succeeds, but GetMethods() also picks up inherited public
            // methods like ToString()/Dispose() which are ignored as MethodDeclaredInObject.
            Service.Build(typeof(HealthyFixture), out var buildReport);
            var report = buildReport.Roots.Single();
            var walked = buildReport.Walk().ToList();
            var methodNode = report.Methods.Single(m => m.Name == "M");
            Assert.AreEqual(BuildReportStatus.Accepted, methodNode.Status);
            CollectionAssert.Contains(walked, methodNode);
            CollectionAssert.Contains(walked, methodNode.Result);
            // type node + M itself are Accepted
            Assert.IsTrue(buildReport.CountByStatus(BuildReportStatus.Accepted) >= 2);
            // inherited object/Service methods are ignored as MethodDeclaredInObject
            Assert.IsTrue(buildReport.CountByStatus(BuildReportStatus.Ignored) >= 1);
            Assert.AreEqual(0, buildReport.CountByStatus(BuildReportStatus.Failed));
        }
    }
}
