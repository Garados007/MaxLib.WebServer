using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;

namespace MaxLib.WebServer.Monitoring
{
    /// <summary>
    /// This enables to trace and monitor the operation inside the handling of a web request
    /// </summary>
    public class Monitor
    {
        /// <summary>
        /// Gets if monitoring for this single request is allowed.
        /// </summary>
        public bool Enabled { get; }

        public IWatch Current { get; internal set; }

        private readonly List<IWatch> watches = new List<IWatch>();

        private static readonly DiscardWatch discard = new DiscardWatch();

        internal readonly Stopwatch monitorWatch;

        public Monitor(bool enabled)
        {
            Enabled = enabled;
            monitorWatch = new Stopwatch();
            if (enabled)
                monitorWatch.Start();
            Current = CreateWatch(null, null);
        }

        public IWatch Watch(object caller, string? info = null)
        {
            _ = caller ?? throw new ArgumentNullException(nameof(caller));
            return CreateWatch(caller, info);
        }

        private IWatch CreateWatch(object? caller, string? info)
        {
            if (Enabled)
            {
                var watch = new MonitoringWatch(this, caller, info, Current);
                watches.Add(watch);
                return Current = watch;
            }
            else return Current = discard;
        }

        public void WriteTo(TextWriter writer)
        {
            foreach (var watch in watches)
                watch.WriteTo(writer);
        }

        public void WriteTo(Utf8JsonWriter writer)
        {
            writer.WriteStartArray();
            foreach (var watch in watches)
                watch.WriteTo(writer);
            writer.WriteEndArray();
        }

        public IEnumerable<IWatchDetails> GetDetails()
        {
            foreach (var watch in watches)
            {
                if (watch is IWatchDetails detail)
                    yield return detail;
            }
        }

        internal async Task Save(string path, DateTime started, WebProgressTask task)
        {
            var format = task.Server?.Settings.MonitoringOutputFormat ?? OutputFormat.TextLog;
            var ext = format switch
            {
                OutputFormat.TextLog => ".log",
                OutputFormat.Json => ".json",
                _ => "",
            };

            var callName = SanitizePath(task.Request.Location.DocumentPath);
            if (callName.Length == 0)
                callName = "_";
            var date = started.ToString("yyyy-MM-dd_HH-mm-ss-fffffff");

            var dir = $"{path}/{callName}";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var inc = 1;
            var file = $"{dir}/{date}{ext}";
            while (File.Exists(file))
            {
                inc++;
                file = $"{dir}/{date}.{inc}{ext}";
            }

            using var stream = new FileStream(file, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            switch (format)
            {
                case OutputFormat.Json:
                    {
                        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
                        {
                            Indented = true,
                        });
                        try { WriteTo(writer); }
                        catch (Exception e)
                        {
                            WebServerLog.Add(ServerLogType.FatalError, GetType(), "write logs", e.ToString());
                        }
                        await writer.FlushAsync();
                        writer.Flush();
                    } break;
                default:
                    {
                        using var writer = new StreamWriter(stream, Encoding.UTF8);
                        try { WriteTo(writer); }
                        catch (Exception e)
                        {
                            WebServerLog.Add(ServerLogType.FatalError, GetType(), "write logs", e.ToString());
                            writer.WriteLine(e);
                        }
                        await writer.FlushAsync();
                        writer.Flush();
                    } break;
            }
            
            await stream.FlushAsync();
        }

        private static char[] allowedChars = new[] { '-', '_', '+', '(', ')', };
        private static string SanitizePath(string path)
        {
            var sb = new StringBuilder(path.Length);
            for (int i = 0; i < path.Length; ++i)
                if (char.IsLetterOrDigit(path[i]) || Array.IndexOf(allowedChars, path[i]) >= 0)
                    sb.Append(path[i]);
                else sb.Append('_');
            return sb.ToString();
        }
    }
}