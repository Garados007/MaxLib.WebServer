using System.Threading.Tasks;
using MaxLib.WebServer.Builder.Runtime;
using MaxLib.WebServer.Builder.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder
{
    [TestClass]
    public class TestRouteSpecificity
    {
        private static async Task<string> Resolve(System.Type fixtureType)
        {
            var group = Generator.GenerateClass(fixtureType)!;
            var task = new WebProgressTask();
            task.Request.Url = "/spec/literal";
            Assert.IsTrue(group.CanWorkWith(task, out ServiceGroup.CallInfo? data),
                "no method matched the request at all");
            await group.ProgressTask(task, data);
            Assert.AreEqual(1, task.Document.DataSources.Count);
            Assert.IsTrue(task.Document.DataSources[0] is HttpStringDataSource);
            return ((HttpStringDataSource)task.Document.DataSources[0]).Data;
        }

        [TestMethod]
        public async Task TestLiteralRouteWinsOverVarRouteWhenVarIsDeclaredFirst()
        {
            Assert.AreEqual("literal", await Resolve(typeof(SpecificityVarFirstFixture)));
        }

        [TestMethod]
        public async Task TestLiteralRouteWinsOverVarRouteWhenLiteralIsDeclaredFirst()
        {
            Assert.AreEqual("literal", await Resolve(typeof(SpecificityLiteralFirstFixture)));
        }
    }
}
