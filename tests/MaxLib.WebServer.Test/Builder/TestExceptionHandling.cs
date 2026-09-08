using System.Linq;
using MaxLib.WebServer.Builder.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder
{
    [TestClass]
    public class TestExceptionHandling
    {
        [TestMethod]
        public async System.Threading.Tasks.Task TestSyncHandlerHttpExceptionProducesTheExpectedStatusCode()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(Generator.GenerateMethod(typeof(SyncHttpExceptionService).GetMethod("Foo")!)!);

            var task = new WebProgressTask();
            task.Request.Url = "/sync-throw";

            await group.Execute(task);

            Assert.AreEqual(HttpStateCode.Forbidden, task.Response.StatusCode);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task TestParameterConversionFailureProducesABadRequestWithAnExplanation()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            group.Add(Generator.GenerateMethod(typeof(ConversionFailureService).GetMethod("Foo")!)!);

            var task = new WebProgressTask();
            task.Request.Url = "/conv-fail?id=abc";

            await group.Execute(task);

            Assert.AreEqual(HttpStateCode.BadRequest, task.Response.StatusCode);
            Assert.AreEqual(1, task.Document.DataSources.Count);
            Assert.IsTrue(task.Document.DataSources[0] is HttpStringDataSource);
            var body = ((HttpStringDataSource)task.Document.DataSources[0]).Data;
            StringAssert.Contains(body, "id");
            StringAssert.Contains(body, "abc");
        }
    }
}
