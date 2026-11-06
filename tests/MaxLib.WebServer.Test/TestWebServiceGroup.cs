using System.Linq;
using System.Threading.Tasks;
using MaxLib.WebServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestWebServiceGroup
    {
        private sealed class RePrioretisableService : WebService
        {
            public RePrioretisableService() : base(ServerStage.CreateDocument) { }

            public void ChangePriority(WebServicePriority newPriority) => Priority = newPriority;

            public override bool CanWorkWith(WebProgressTask task) => false;
            public override Task ProgressTask(WebProgressTask task) => Task.CompletedTask;
        }

        [TestMethod]
        public void TestClearDetachesFromPriorityChanged()
        {
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            var service = new RePrioretisableService();
            group.Add(service);
            Assert.AreEqual(1, group.GetAll<WebService>().Count());

            group.Clear();
            Assert.AreEqual(0, group.GetAll<WebService>().Count());

            // Before the fix, the handler was still attached: changing the priority
            // called Services.ChangePriority(...) and put the service back into the
            // list, even though Clear() had just emptied it.
            service.ChangePriority(WebServicePriority.High);

            Assert.AreEqual(0, group.GetAll<WebService>().Count(),
                "a service removed by Clear() must not be re-added by a priority change");
        }

        [TestMethod]
        public void TestRemoveDetachesFromPriorityChanged()
        {
            // This already worked, but pinning it down guards against a future
            // refactor of Clear() that shares a helper with Remove() and breaks one
            // of them.
            var group = new WebServiceGroup(ServerStage.CreateDocument);
            var service = new RePrioretisableService();
            group.Add(service);
            Assert.IsTrue(group.Remove(service));

            service.ChangePriority(WebServicePriority.Low);

            Assert.AreEqual(0, group.GetAll<WebService>().Count());
        }
    }
}