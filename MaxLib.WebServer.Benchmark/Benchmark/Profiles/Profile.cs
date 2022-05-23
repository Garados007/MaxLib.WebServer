using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Benchmark.Profiles
{
    public class Profile
    {
        public Dictionary<ProfileEntryId, List<TimeSpan>> Entries { get; }

        public Dictionary<ProfileLogId, List<TimeSpan>> Logs { get; }

        public Profile()
        {
            Entries = new Dictionary<ProfileEntryId, List<TimeSpan>>();
            Logs = new Dictionary<ProfileLogId, List<TimeSpan>>();
        }

        public void Add(Monitoring.Monitor monitor)
        {
            var entryIds = new Dictionary<(string, string), int>();
            var logIds = new Dictionary<(ProfileEntryId, string), int>();
            foreach (var entry in monitor.GetDetails())
            {
                if (entry is null)
                    continue;
                
                var entryKey = (entry.Caller?.GetType().FullName ?? "", entry.Info ?? "");
                if (!entryIds.TryGetValue(entryKey, out int id))
                    entryIds.Add(entryKey, id = 1);
                else entryIds[entryKey] = ++id;
                var entryId = new ProfileEntryId(entryKey.Item1, entryKey.Item2, id);

                if (!Entries.TryGetValue(entryId, out List<TimeSpan>? times))
                    Entries.Add(entryId, times = new List<TimeSpan>());
                times.Add(entry.Elapsed);

                foreach (var log in entry.GetLogs())
                {
                    var logKey = (entryId, log.format);
                    if (!logIds.TryGetValue(logKey, out id))
                        logIds.Add(logKey, id = 1);
                    else logIds[logKey] = ++id;
                    var logId = new ProfileLogId(entryId, log.format, id);

                    if (!Logs.TryGetValue(logId, out  times))
                        Logs.Add(logId, times = new List<TimeSpan>());
                    times.Add(log.elapsed);
                }

            }

        }
    
        public ProfileStats ToStats()
        {
            var stats = new ProfileStats();
            foreach (var (key, list) in Entries)
            {
                var stat = Stat.Create(list);
                if (stat != null)
                    stats.Entries.Add(key, stat.Value);
            }
            foreach (var (key, list) in Logs)
            {
                var stat = Stat.Create(list);
                if (stat != null)
                    stats.Logs.Add(key, stat.Value);
            }
            return stats;
        }
    }
}