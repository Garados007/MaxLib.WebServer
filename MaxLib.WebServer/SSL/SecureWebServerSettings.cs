using System;
using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace MaxLib.WebServer.SSL
{
    public class SecureWebServerSettings : WebServerSettings
    {
        public int SecurePort { get; private set; }
        public bool EnableUnsafePort { get; set; } = true;

        public X509Certificate? Certificate { get; set; }

        /// <summary>
        /// The maximum time the TLS handshake may take before the connection is dropped.
        /// Defaults to 10 seconds; zero or a negative value disables the timeout.
        /// </summary>
        public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(10);

        public SecureWebServerSettings(int port, int securePort, int connectionTimeout)
            : base(port, connectionTimeout)
        {
            SecurePort = securePort;
        }

        public SecureWebServerSettings(int securePort, int connectionTimeout)
            : base(80, connectionTimeout)
        {
            SecurePort = securePort;
            EnableUnsafePort = false;
        }
    }
}
