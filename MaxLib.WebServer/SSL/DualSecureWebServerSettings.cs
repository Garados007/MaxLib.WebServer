using System;
using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace MaxLib.WebServer.SSL
{
    public class DualSecureWebServerSettings : WebServerSettings
    {
        public X509Certificate Certificate { get; set; }

        /// <summary>
        /// The maximum time the protocol-detection peek and the TLS handshake may each take before the connection
        /// is dropped. Defaults to 10 seconds; zero or a negative value disables the timeout.
        /// </summary>
        public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <param name="port">The port to listen on.</param>
        /// <param name="connectionTimeout">The connection timeout in milliseconds.</param>
        /// <param name="certificate">Must not be null; a null certificate makes every HTTPS handshake fail at connection time.</param>
        public DualSecureWebServerSettings(int port, int connectionTimeout, X509Certificate certificate)
            : base(port, connectionTimeout)
        {
            Certificate = certificate;
        }
    }
}
