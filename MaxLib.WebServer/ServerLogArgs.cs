using System;

#nullable enable

namespace MaxLib.WebServer
{
    public class ServerLogArgs : EventArgs
    {
        public ServerLogItem LogItem { get; }

        public bool Discard { get; set; } = false;

        public ServerLogArgs(ServerLogItem logItem)
            => LogItem = logItem;
    }
}
