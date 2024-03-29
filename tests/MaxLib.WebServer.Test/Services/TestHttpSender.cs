﻿using MaxLib.WebServer.Services;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Test.Services
{
    [TestClass]
    public class TestHttpSender
    {
        TestWebServer server;
        TestTask test;

        [TestInitialize]
        public void Init()
        {
            server = new TestWebServer();
            server.AddWebService(new HttpSender());
            test = new TestTask(server)
            {
                CurrentStage = ServerStage.SendResponse,
                TerminationStage = ServerStage.SendResponse,
            };
        }

        [TestMethod]
        public async Task TestSending()
        {
            test.Response.HttpProtocol = HttpProtocolDefinition.HttpVersion1_1;
            test.Response.StatusCode = HttpStateCode.OK;
            test.Response.FieldContentType = MimeType.TextPlain;
            test.Request.Cookie.AddedCookies.Add("test",
                new HttpCookie.Cookie("test", "value"));
            test.Task.Document.DataSources.Add(
                new HttpStringDataSource("foobarbaz\r\n"));

            using (var response = test.SetStream())
            using (var r = new StreamReader(response))
            {
                await new HttpSender().ProgressTask(test.Task).ConfigureAwait(false);

                response.Position = 0;

                Assert.AreEqual("HTTP/1.1 200 OK", r.ReadLine());
                Assert.AreEqual("Content-Type: text/plain", r.ReadLine());
                Assert.AreEqual("Set-Cookie: test=value;Path=", r.ReadLine());
                Assert.AreEqual("", r.ReadLine());
                Assert.AreEqual("foobarbaz", r.ReadLine());
            }
        }
    }
}
