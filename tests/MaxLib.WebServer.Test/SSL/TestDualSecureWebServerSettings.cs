using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MaxLib.WebServer.SSL;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MaxLib.WebServer.Test.SSL
{
    [TestClass]
    public class TestDualSecureWebServerSettings
    {
        [TestMethod]
        public void TestHandshakeTimeoutDefaultsToTenSeconds()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=maxlib-webserver-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1
            );
            using var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));

            var settings = new DualSecureWebServerSettings(443, 5000, certificate);

            Assert.AreEqual(TimeSpan.FromSeconds(10), settings.HandshakeTimeout);
        }
    }
}
