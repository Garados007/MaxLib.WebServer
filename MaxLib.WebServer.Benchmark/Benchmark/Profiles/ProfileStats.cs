using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Benchmark.Profiles
{
    public class ProfileStats
    {
        public Dictionary<ProfileEntryId, Stat> Entries { get; }

        public Dictionary<ProfileLogId, Stat> Logs { get; }

        public ProfileStats()
        {
            Entries = new Dictionary<ProfileEntryId, Stat>();
            Logs = new Dictionary<ProfileLogId, Stat>();
        }

        private Dictionary<TKey, (TValue?, TValue?)> Merge<TKey, TValue>(
            Dictionary<TKey, TValue> dict1, 
            Dictionary<TKey, TValue> dict2
        )
            where TValue : struct
        {
            var result = new Dictionary<TKey, (TValue?, TValue?)>();
            foreach (var (key, value) in dict1)
            {
                result.Add(key, (value, null));
            }
            foreach (var (key, value) in dict2)
            {
                if (result.TryGetValue(key, out (TValue? existing, TValue?) t))
                {
                    result[key] = (t.existing, value);
                }
                else result.Add(key, (null, value));
            }
            return result;
        }

        public Rendering.Table ToEntryTable()
        {
            var table = RenderTable(Entries, 3, 
                (y, key, t) =>
                {
                    t.Body[0, y] = key.Key.type;
                    t.Body[1, y] = key.Key.info;
                    t.Body[2, y] = key.Increment.ToString();
                }
            );
            table.Alignment.SetColumn(0, Rendering.Alignment.Left);
            table.Alignment.SetColumn(1, Rendering.Alignment.Left);
            table.Header[0].Text = "Type";
            table.Header[1].Text = "Info";
            table.Header[2].Text = "No";
            return table;
        }

        public Rendering.Table ToLogTable()
        {
            var table = RenderTable(Logs, 5, 
                (y, key, t) =>
                {
                    t.Body[0, y] = key.Key.entry.Key.type;
                    t.Body[1, y] = key.Key.entry.Key.info;
                    t.Body[2, y] = key.Key.entry.Increment.ToString();
                    t.Body[3, y] = key.Key.format;
                    t.Body[4, y] = key.Increment.ToString();
                }
            );
            table.Alignment.SetColumn(0, Rendering.Alignment.Left);
            table.Alignment.SetColumn(1, Rendering.Alignment.Left);
            table.Header[0].Text = "Type";
            table.Header[1].Text = "Info";
            table.Header[2].Text = "No";
            table.Header[3].Text = "Format";
            table.Header[4].Text = "No";
            return table;
        }

        public Rendering.Table ToEntryTable(
            ProfileStats second,
            string firstName = "1st",
            string secondName = "2nd"
        )
        {
            var entries = Merge(Entries, second.Entries);
            var table = RenderTable(entries, 3, (y, key, t) =>
            {
                t.Body[0, y] = key.Key.type;
                t.Body[1, y] = key.Key.info;
                t.Body[2, y] = key.Increment.ToString();
            }, firstName, secondName);
            table.Alignment.SetColumn(2, Rendering.Alignment.Right);
            table.Header[0, 0].Text = "Type";
            table.Header[1, 0].Text = "Info";
            table.Header[2, 0].Text = "No";
            return table;
        }

        public Rendering.Table ToLogTable(
            ProfileStats second,
            string firstName = "1st",
            string secondName = "2nd"
        )
        {
            var entries = Merge(Logs, second.Logs);
            var table = RenderTable(entries, 5, 
                (y, key, t) =>
                {
                    t.Body[0, y] = key.Key.entry.Key.type;
                    t.Body[1, y] = key.Key.entry.Key.info;
                    t.Body[2, y] = key.Key.entry.Increment.ToString();
                    t.Body[3, y] = key.Key.format;
                    t.Body[4, y] = key.Increment.ToString();
                },
                firstName,
                secondName
            );
            table.Alignment.SetColumn(0, Rendering.Alignment.Left);
            table.Alignment.SetColumn(1, Rendering.Alignment.Left);
            table.Header[0].Text = "Type";
            table.Header[1].Text = "Info";
            table.Header[2].Text = "No";
            table.Header[3].Text = "Format";
            table.Header[4].Text = "No";
            return table;
        }

        private Rendering.Table RenderTable<T>(
            Dictionary<T, Stat> dict,
            int keySize,
            Action<int, T, Rendering.Table> renderKey
        )
        {
            var table = new Rendering.Table(4 + keySize, Entries.Count);
            table.Header.SetRowAlignment(0, Rendering.Alignment.Center);
            table.Alignment.Set(Rendering.Alignment.Right);
            table.Header[0 + keySize].Text = "Min";
            table.Header[1 + keySize].Text = "Max";
            table.Header[2 + keySize].Text = "Avg";
            table.Header[3 + keySize].Text = "Mean";
            var y = 0;
            foreach (var (key, value) in dict)
            {
                renderKey(y, key, table);
                table.Body[0 + keySize, y] = value.Min.ToString("g");
                table.Body[1 + keySize, y] = value.Max.ToString("g");
                table.Body[2 + keySize, y] = value.Avg.ToString("g");
                table.Body[3 + keySize, y] = value.Mean.ToString("g");
                y++;
            }
            return table;
        }

        private Rendering.Table RenderTable<T>(
            Dictionary<T, (Stat?, Stat?)> dict,
            int keySize,
            Action<int, T, Rendering.Table> renderKey,
            string firstName,
            string secondName
        )
        {
            var table = new Rendering.Table(8 + keySize, dict.Count, 2, 1);
            table.Header.SetRowAlignment(1, Rendering.Alignment.Center);
            table.Header[0 + keySize, 0] = new Rendering.TableHeaderCell("Min",  2, Rendering.Alignment.Center);
            table.Header[2 + keySize, 0] = new Rendering.TableHeaderCell("Max",  2, Rendering.Alignment.Center);
            table.Header[4 + keySize, 0] = new Rendering.TableHeaderCell("Avg",  2, Rendering.Alignment.Center);
            table.Header[6 + keySize, 0] = new Rendering.TableHeaderCell("Mean", 2, Rendering.Alignment.Center);
            for (int i = 0; i < 4; ++i)
            {
                table.Header[keySize + i * 2, 1].Text = firstName;
                table.Header[keySize + 1 + i * 2, 1].Text = secondName;
            }
            var y = 0;
            Span<double> sum = stackalloc double[4];
            int count = 0;
            foreach (var (key, (fst, snd)) in dict)
            {
                renderKey(y, key, table);
                if (fst != null)
                {
                    table.Body[0 + keySize, y] = fst.Value.Min.ToString("g");
                    table.Body[2 + keySize, y] = fst.Value.Max.ToString("g");
                    table.Body[4 + keySize, y] = fst.Value.Avg.ToString("g");
                    table.Body[6 + keySize, y] = fst.Value.Mean.ToString("g");
                }

                if (snd != null)
                {
                    table.Body[1 + keySize, y] = snd.Value.Min.ToString("g");
                    table.Body[3 + keySize, y] = snd.Value.Max.ToString("g");
                    table.Body[5 + keySize, y] = snd.Value.Avg.ToString("g");
                    table.Body[7 + keySize, y] = snd.Value.Mean.ToString("g");
                }

                if (fst != null && snd != null)
                {
                    sum[0] += snd.Value.Min.TotalSeconds / fst.Value.Min.TotalSeconds;
                    sum[1] += snd.Value.Max.TotalSeconds / fst.Value.Max.TotalSeconds;
                    sum[2] += snd.Value.Avg.TotalSeconds / fst.Value.Avg.TotalSeconds;
                    sum[3] += snd.Value.Mean.TotalSeconds / fst.Value.Mean.TotalSeconds;
                    count++;
                }
                y++;
            }
            if (count > 0)
            {
                for (int i = 0; i < 4; ++i)
                    table.Footer[keySize + i * 2] = new Rendering.TableHeaderCell(
                        $"{sum[i]/count:#,#0.00%}", 
                        2,
                        Rendering.Alignment.Center
                    );
            }
            return table;
        }
    }
}