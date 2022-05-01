using System;
using System.IO;

namespace MaxLib.WebServer.Monitoring
{
    public interface IWatch : IDisposable
    {
        TimeSpan Elapsed { get; }

        void WriteTo(TextWriter writer);

        void Log(string format, params object[] args);

        IWatch? Parent { get; }
    }
}