using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Monitoring
{
    public interface IWatchDetails : IWatch
    {
        TimeSpan Started { get; }

        object? Caller { get; }

        string? Info { get; }

        IEnumerable<(TimeSpan elapsed, string format, object[] args)> GetLogs();
    }
}