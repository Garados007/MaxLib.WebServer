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

        /// <summary>
        /// Specify where the monitoring output should be written to. Set it to <c>null</c> to
        /// disable monitoring (this is the default setting).
        /// </summary>
        public string? MonitoringOutputDirectory { get; set; }

        TimeSpan connectionDelay = TimeSpan.FromMilliseconds(20);
        /// <summary>
        /// The time after which the server should check for new incomming connections or requests.
        /// Keep it to a reasonable value to reduce "lags" for the user. <br/> If you are building a
        /// high performance server with lowest lag possible set it to <see cref="TimeSpan.Zero" />.
        /// Setting to this is not recommended because this will increase the cpu usage heavily.
        /// <br/> The supported range is 0 ms to 1 second. The default value is 20 ms.
        /// </summary>
        public TimeSpan ConnectionDelay
        {
            get => connectionDelay;
            set
            {
                if (value < TimeSpan.Zero || value > TimeSpan.FromSeconds(1))
                    throw new ArgumentOutOfRangeException(nameof(ConnectionDelay), value, "supported range is 0ms to 1s");
                connectionDelay = value;
            }
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
