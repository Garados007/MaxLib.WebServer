using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace MaxLib.WebServer.SSL
{
    public class DualSecureWebServerSettings : WebServerSettings
    {
        public X509Certificate? Certificate { get; set; }

        public DualSecureWebServerSettings(string settingFolderPath)
            : base(settingFolderPath)
        {

        }

        public DualSecureWebServerSettings(int port, int connectionTimeout, X509Certificate certificate)
            : base(port, connectionTimeout)
        {
            Certificate = certificate;
        }
    }
}
