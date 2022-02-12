using System.Linq;
using System.Reflection;
using MaxLib.WebServer.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Test.Builder
{
    [TestClass]
    public class TestSingleService
    {
        [TestMethod]
        public void TestMethod()
        {
            var method = MaxLib.WebServer.Builder.Tools.Generator.GenerateMethod(
                typeof(SingleService).GetMethod("Foo")!
            );
            Assert.IsNotNull(method);
            Assert.AreEqual(1, method!.Rules.Count);
            Assert.IsTrue(method.Rules[0] is PathAttribute);
            Assert.AreEqual(3, method.Parameters.Count);
            Assert.IsNotNull(method.Result);
            Assert.IsTrue(method.MethodClass is SingleService);
            Assert.AreEqual(WebServicePriority.High, method.Priority);
        }

        [TestMethod]
        public void TestResult()
        {
            var result = MaxLib.WebServer.Builder.Tools.Generator.GenerateResult(
                typeof(SingleService).GetMethod("Foo")!
            );
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void TestResult2()
        {
            Assert.IsNotNull(MaxLib.WebServer.Builder.Tools.Generator.GenerateResult(
                new MaxLib.WebServer.Builder.Converter.DataConverter(),
                typeof(HttpDataSource)
            ));
            Assert.IsNotNull(MaxLib.WebServer.Builder.Tools.Generator.GenerateResult(
                new MaxLib.WebServer.Builder.Converter.DataConverter(),
                typeof(Task<HttpDataSource>)
            ));
        }

        [TestMethod]
        public void TestClass()
        {
            var group = MaxLib.WebServer.Builder.Tools.Generator.GenerateClass(typeof(SingleService));
            Assert.IsNotNull(group);
            Assert.AreEqual(1, group!.Rules.Count);
            Assert.IsTrue(group.Rules[0] is PathAttribute);
            Assert.AreEqual(1, group.Count);
            var service = group.First();
            Assert.IsNotNull(service);
            Assert.IsTrue(service is MaxLib.WebServer.Builder.Runtime.MethodService);
            var method = (MaxLib.WebServer.Builder.Runtime.MethodService)service;
            Assert.AreEqual(typeof(SingleService).GetMethod("Foo")!, method.Method);
        }

        [TestMethod]
        public async Task TestMethodExec()
        {
            var method = MaxLib.WebServer.Builder.Tools.Generator.GenerateMethod(
                typeof(SingleService).GetMethod("Foo")!
            );
            var task = new WebProgressTask();
            task.Request.Url = "/foo/2?bar=bar";
            Assert.IsTrue(method!.CanWorkWith(task, out object?[]? data));
            await method.ProgressTask(task, data);
            Assert.AreEqual(1, task.Document.DataSources.Count);
            Assert.IsTrue(task.Document.DataSources[0] is HttpStringDataSource);
            var source = (HttpStringDataSource)task.Document.DataSources[0];
            Assert.AreEqual("test", source.Data);
        }
    }
}
