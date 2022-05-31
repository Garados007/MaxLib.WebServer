using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace MaxLib.WebServer.Monitoring
{
    internal class MonitoringWatch : IWatch
    {
        private readonly TimeSpan started;

        public object? Caller { get; }

        private TimeSpan? elapsed;
        public TimeSpan Elapsed => elapsed ?? (Monitor.monitorWatch.Elapsed - started);

        public string? Info { get; }

        public IWatch? Parent { get; }

        public Monitor Monitor { get; }

        private readonly List<(TimeSpan elapsed, string format, object[] args)> logs;

        public MonitoringWatch(Monitor monitor, object? caller, string? info, IWatch? parent)
        {
            Monitor = monitor;
            Caller = caller;
            Info = info;
            Parent = parent;
            logs = new List<(TimeSpan elapsed, string format, object[] args)>();
            started = monitor.monitorWatch.Elapsed;
        }

        public void Dispose()
        {
            elapsed = Monitor.monitorWatch.Elapsed - started;
        }

        private static readonly Dictionary<Type, Dictionary<object, int>> ids
            = new Dictionary<Type, Dictionary<object, int>>();


        public void WriteTo(TextWriter writer)
        {
            if (Caller is null)
            {
                writer.WriteLine($"[{started:G}] [{Elapsed:G}] ROOT {Info}");
            }
            else
            {
                var type = Caller.GetType();
                if (!ids.TryGetValue(type, out Dictionary<object, int>? typeDict))
                    ids.Add(type, typeDict = new Dictionary<object, int>());
                if (!typeDict.TryGetValue(Caller, out int id))
                    typeDict.Add(Caller, id = typeDict.Count + 1);
                var name = type.FullName ?? "";
                if (name.StartsWith("MaxLib.WebServer"))
                    name = $"<{type.Name}>";

                writer.WriteLine($"[{started:G}] [{Elapsed:G}] {name} #{id} {Info}");
            }
            foreach (var (elapsed, format, args) in logs)
                writer.WriteLine($"\t[{elapsed:G}] {format}", args);
        }

        public void Log(string format, params object[] args)
        {
            logs.Add((Elapsed, format, args));
        }
    }
}