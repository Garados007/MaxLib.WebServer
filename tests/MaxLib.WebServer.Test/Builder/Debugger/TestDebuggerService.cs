using System.Linq;
using System.Text.Json;
using MaxLib.WebServer.Builder;
using MaxLib.WebServer.Builder.Debugger;
using MaxLib.WebServer.Builder.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder.Debugger
{
    [TestClass]
    public class TestDebuggerService
    {
        private static (Server server, DebuggerService debugger) CreateServer()
        {
            var server = new Server(new WebServerSettings(12345, 0));
            server.AddWebService(Generator.GenerateMethod(typeof(GetOnlyFixture).GetMethod("M")!)!);
            Service.Build(typeof(HealthyFixture), out var report);
            var debugger = new DebuggerService(server, report);
            server.AddWebService(debugger);
            return (server, debugger);
        }

        private static WebProgressTask MakeTask(string url)
        {
            var task = new WebProgressTask();
            task.Request.Url = url;
            return task;
        }

        private static string GetBody(WebProgressTask task)
        {
            Assert.AreEqual(1, task.Document.DataSources.Count);
            return Assert.IsInstanceOfType<HttpStringDataSource>(task.Document.DataSources[0]).Data;
        }

        [TestMethod]
        public void TestCanWorkWithMatchesOnlyOwnSubTree()
        {
            var (_, debugger) = CreateServer();

            Assert.IsTrue(debugger.CanWorkWith(MakeTask("/_debugger")));
            Assert.IsTrue(debugger.CanWorkWith(MakeTask("/_debugger/")));
            Assert.IsTrue(debugger.CanWorkWith(MakeTask("/_debugger/api/build-report")));
            Assert.IsFalse(debugger.CanWorkWith(MakeTask("/_debuggerother")));
            Assert.IsFalse(debugger.CanWorkWith(MakeTask("/other")));
        }

        [TestMethod]
        public async System.Threading.Tasks.Task TestHtmlShellIsServed()
        {
            var (_, debugger) = CreateServer();
            var task = MakeTask("/_debugger/");
            await debugger.ProgressTask(task);

            Assert.AreEqual(HttpStateCode.OK, task.Response.StatusCode);
            Assert.AreEqual(MimeType.TextHtml, task.Document.DataSources[0].MimeType);
            var body = GetBody(task);
            StringAssert.Contains(body, "__debuggerBasePath");
            StringAssert.Contains(body, "/api/build-report");
        }

        [TestMethod]
        public async System.Threading.Tasks.Task TestBuildReportJsonIsServed()
        {
            var (_, debugger) = CreateServer();
            var task = MakeTask("/_debugger/api/build-report");
            await debugger.ProgressTask(task);

            Assert.AreEqual(MimeType.ApplicationJson, task.Document.DataSources[0].MimeType);
            var body = GetBody(task);
            using var doc = JsonDocument.Parse(body);
            var roots = doc.RootElement.GetProperty("roots");
            Assert.AreEqual(1, roots.GetArrayLength());
            Assert.AreEqual(nameof(HealthyFixture), roots[0].GetProperty("name").GetString());
        }

        [TestMethod]
        public async System.Threading.Tasks.Task TestRouteJsonIsServedForOneStage()
        {
            var (_, debugger) = CreateServer();
            var task = MakeTask("/_debugger/api/route?method=GET&path=/only-get&stage=CreateDocument");
            await debugger.ProgressTask(task);

            var body = GetBody(task);
            using var doc = JsonDocument.Parse(body);
            Assert.AreEqual(1, doc.RootElement.GetArrayLength());
            Assert.AreEqual("createDocument", doc.RootElement[0].GetProperty("stage").GetString());
            Assert.AreEqual("GetOnlyFixture.M()", doc.RootElement[0].GetProperty("matchedLabel").GetString());
        }

        [TestMethod]
        public async System.Threading.Tasks.Task TestRouteJsonSimulatesAllStagesByDefault()
        {
            var (_, debugger) = CreateServer();
            var task = MakeTask("/_debugger/api/route?method=GET&path=/only-get");
            await debugger.ProgressTask(task);

            var body = GetBody(task);
            using var doc = JsonDocument.Parse(body);
            Assert.AreEqual(7, doc.RootElement.GetArrayLength());
            var createDocument = doc.RootElement.EnumerateArray()
                .Single(e => e.GetProperty("stage").GetString() == "createDocument");
            Assert.AreEqual("GetOnlyFixture.M()", createDocument.GetProperty("matchedLabel").GetString());
        }

        [TestMethod]
        public async System.Threading.Tasks.Task TestUnknownSubPathIsNotFound()
        {
            var (_, debugger) = CreateServer();
            var task = MakeTask("/_debugger/api/does-not-exist");
            await debugger.ProgressTask(task);

            Assert.AreEqual(HttpStateCode.NotFound, task.Response.StatusCode);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task TestMissingBuildReportServesEmptyReport()
        {
            var server = new Server(new WebServerSettings(12346, 0));
            var debugger = new DebuggerService(server);
            var task = MakeTask("/_debugger/api/build-report");
            await debugger.ProgressTask(task);

            var body = GetBody(task);
            using var doc = JsonDocument.Parse(body);
            Assert.AreEqual(0, doc.RootElement.GetProperty("roots").GetArrayLength());
        }
    }
}
