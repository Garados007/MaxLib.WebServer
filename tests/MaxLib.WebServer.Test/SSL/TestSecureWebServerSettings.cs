using System;
using MaxLib.WebServer.SSL;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MaxLib.WebServer.Test.SSL
{
    [TestClass]
    public class TestSecureWebServerSettings
    {
        [TestMethod]
        public void TestHandshakeTimeoutDefaultsToTenSeconds()
        {
            var settings = new SecureWebServerSettings(443, 5000);
            Assert.AreEqual(TimeSpan.FromSeconds(10), settings.HandshakeTimeout);
        }
    }
}
