using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.Json;

namespace MaxLib.WebServer.Monitoring
{
    internal class MonitoringWatch : IWatch, IWatchDetails
    {
        public TimeSpan Started { get; }

        public object? Caller { get; }

        private TimeSpan? elapsed;
        public TimeSpan Elapsed => elapsed ?? (Monitor.monitorWatch.Elapsed - Started);

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
            Started = monitor.monitorWatch.Elapsed;
        }

        public void Dispose()
        {
            elapsed = Monitor.monitorWatch.Elapsed - Started;
        }

        private static readonly Dictionary<Type, Dictionary<object, int>> ids
            = new Dictionary<Type, Dictionary<object, int>>();


        public void WriteTo(TextWriter writer)
        {
            if (Caller is null)
            {
                writer.WriteLine($"[{Started:G}] [{Elapsed:G}] ROOT {Info}");
            }
            else
            {
                var type = Caller.GetType();
                if (!ids.TryGetValue(type, out Dictionary<object, int> typeDict))
                    ids.Add(type, typeDict = new Dictionary<object, int>());
                if (!typeDict.TryGetValue(Caller, out int id))
                    typeDict.Add(Caller, id = typeDict.Count + 1);
                var name = type.FullName;
                if (name.StartsWith("MaxLib.WebServer"))
                    name = $"<{type.Name}>";

                writer.WriteLine($"[{Started:G}] [{Elapsed:G}] {name} #{id} {Info}");
            }
            foreach (var (elapsed, format, args) in logs)
                writer.WriteLine($"\t[{elapsed:G}] {format}", args);
        }

        public void WriteTo(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            if (Caller is null)
            {
                writer.WriteNull("caller");
            }
            else
            {
                var type = Caller.GetType();
                if (!ids.TryGetValue(type, out Dictionary<object, int> typeDict))
                    ids.Add(type, typeDict = new Dictionary<object, int>());
                if (!typeDict.TryGetValue(Caller, out int id))
                    typeDict.Add(Caller, id = typeDict.Count + 1);

                writer.WriteStartObject("caller");
                writer.WriteString("name", type.FullName);
                writer.WriteBoolean("core", type.FullName.StartsWith("MaxLib.WebServer"));
                writer.WriteNumber("id", id);
                writer.WriteEndObject();
            }
            writer.WriteNumber("started", Started.TotalSeconds);
            writer.WriteNumber("elapsed", Elapsed.TotalSeconds);
            writer.WriteString("info", Info);
            writer.WriteStartArray("logs");
            foreach (var log in logs)
            {
                writer.WriteStartObject();
                writer.WriteNumber("time", log.elapsed.TotalSeconds);
                writer.WriteString("format", log.format);
                writer.WriteStartArray("args");
                foreach (var arg in log.args)
                {
                    JsonElement? node;
                    try { node = JsonSerializer.SerializeToElement(arg); }
                    catch { node = null; }
                    if (node != null)
                        node.Value.WriteTo(writer);
                    else writer.WriteStringValue(arg.ToString());
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        public void Log(string format, params object[] args)
        {
            logs.Add((Elapsed, format, args));
        }

        public IEnumerable<(TimeSpan elapsed, string format, object[] args)> GetLogs()
        {
            return logs;
        }
    }
}