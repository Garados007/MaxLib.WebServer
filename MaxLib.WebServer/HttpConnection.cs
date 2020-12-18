using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpConnection
    {
        public string? Ip { get; set; }

        public TcpClient? NetworkClient { get; set; }

        public Stream? NetworkStream { get; set; }

        public int LastWorkTime { get; set; } = -1;
    }
}
