using MaxLib.WebServer.Services;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Test.Services
{
    [TestClass]
    public class TestHttpHeaderSpecialAction
    {
        TestWebServer server;
        TestTask test;

        [TestInitialize]
        public void Init()
        {
            server = new TestWebServer();
            server.AddWebService(new HttpHeaderSpecialAction());
            test = new TestTask(server)
            {
                CurrentStage = ServerStage.ParseRequest,
                TerminationStage = ServerStage.ParseRequest,
            };
        }

        [TestMethod]
        public async Task TestHead()
        {
            test.Request.ProtocolMethod = HttpProtocollMethod.Head;
            await new HttpHeaderSpecialAction().ProgressTask(test.Task).ConfigureAwait(false);
            Assert.AreEqual(true, test.GetInfoObject("Only Header"));
        }

        [TestMethod]
        public async Task TestOptions()
        {
            test.Request.ProtocolMethod = HttpProtocollMethod.Options;
            await new HttpHeaderSpecialAction().ProgressTask(test.Task).ConfigureAwait(false);
            Assert.AreEqual(1, test.GetDataSources().Count);
            Assert.IsTrue(test.GetDataSources()[0] is HttpStringDataSource);
            var dataSource = (HttpStringDataSource)test.GetDataSources()[0];
            Assert.IsNotNull(dataSource.Data);
            Assert.IsTrue(dataSource.Data.Length > 0);
        }
    }
}
