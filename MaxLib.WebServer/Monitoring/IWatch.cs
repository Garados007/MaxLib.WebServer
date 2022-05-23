using System;
using System.IO;
using System.Text.Json;

namespace MaxLib.WebServer.Monitoring
{
    public interface IWatch : IDisposable
    {
        TimeSpan Elapsed { get; }

        void WriteTo(TextWriter writer);

        void WriteTo(Utf8JsonWriter writer);

        void Log(string format, params object[] args);

        IWatch? Parent { get; }
    }
}