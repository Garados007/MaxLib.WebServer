using System;
using System.Net;

#nullable enable

namespace MaxLib.WebServer
{
    public class WebServerSettings
    {
        public int Port { get; private set; }

        public int ConnectionTimeout { get; private set; }

        IPAddress ipFilter = IPAddress.Any;
        public IPAddress IPFilter
        {
            get => ipFilter;
            set => ipFilter = value ?? throw new ArgumentNullException(nameof(IPFilter));
        }

        //Debug
        public bool Debug_WriteRequests = false;
        public bool Debug_LogConnections = false;

        public WebServerSettings(int port, int connectionTimeout)
        {
            if (port <= 0 || port >= 0xffff)
                throw new ArgumentOutOfRangeException(nameof(port));
            if (connectionTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(connectionTimeout));
            Port = port;
            ConnectionTimeout = connectionTimeout;
        }
    }
}
