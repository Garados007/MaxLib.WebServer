using MaxLib.WebServer.Services;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Test.Services
{
    [TestClass]
    public class TestHttpRequestHeader
    {
        TestWebServer server;
        TestTask test;

        [TestInitialize]
        public void Init()
        {
            server = new TestWebServer();
            test = new TestTask(server)
            {
                CurrentStage = ServerStage.ParseRequest,
                TerminationStage = ServerStage.ParseRequest,
            };
        }

        [TestMethod]
        public void TestAccept()
        {
            test.Request.HeaderParameter.Add("Accept", "text/css,*/*;q=0.1");
            Assert.AreEqual(2, test.Request.FieldAccept.Count);
            Assert.AreEqual("text/css", test.Request.FieldAccept[0]);
            Assert.AreEqual("*/*;q=0.1", test.Request.FieldAccept[1]);
        }

        [TestMethod]
        public void TestAcceptEncoding()
        {
            test.Request.HeaderParameter.Add("Accept-Encoding", "gzip, deflate, br");
            Assert.AreEqual(3, test.Request.FieldAcceptEncoding.Count);
            Assert.AreEqual("gzip", test.Request.FieldAcceptEncoding[0]);
            Assert.AreEqual("deflate", test.Request.FieldAcceptEncoding[1]);
            Assert.AreEqual("br", test.Request.FieldAcceptEncoding[2]);
        }

        [TestMethod]
        public void TestConnection()
        {
            test.Request.HeaderParameter.Add("Connection", "keep-alive");
            Assert.AreEqual(HttpConnectionType.KeepAlive, test.Request.FieldConnection);
        }

        [TestMethod]
        public void TestConnectionClose()
        {
            test.Request.HeaderParameter.Add("Connection", "close");
            Assert.AreEqual(HttpConnectionType.Close, test.Request.FieldConnection);
        }

        [TestMethod]
        public void TestHost()
        {
            test.Request.HeaderParameter.Add("Host", "test.domain");
            Assert.AreEqual("test.domain", test.Request.Host);
        }

        [TestMethod]
        public void TestCookie()
        {
            test.Request.HeaderParameter.Add("Cookie", "key1=value1; key2= value2;");
            Assert.AreEqual("key1=value1; key2= value2;", test.Request.Cookie.CompleteRequestCookie);
        }
    }
}
