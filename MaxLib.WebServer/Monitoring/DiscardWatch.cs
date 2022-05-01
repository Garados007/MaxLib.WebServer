using System;
using System.IO;

namespace MaxLib.WebServer.Monitoring
{
    public class DiscardWatch : IWatch
    {
        public TimeSpan Elapsed => TimeSpan.Zero;

        public IWatch? Parent => null;

        public DiscardWatch()
        {
        }

        public void Dispose()
        {
        }

        public void Log(string format, params object[] args)
        {
        }

        public void WriteTo(TextWriter writer)
        {
            throw new NotSupportedException("no logs exists to write");
        }
    }
}