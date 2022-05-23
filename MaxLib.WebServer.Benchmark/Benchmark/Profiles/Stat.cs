using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Benchmark.Profiles
{
    public readonly struct Stat
    {
        public Stat(TimeSpan min, TimeSpan max, TimeSpan avg, TimeSpan mean)
        {
            Min = min;
            Max = max;
            Avg = avg;
            Mean = mean;
        }

        public TimeSpan Min { get; }

        public TimeSpan Max { get; }

        public TimeSpan Avg { get; }

        public TimeSpan Mean { get; }

        public override bool Equals(object? obj)
        {
            return obj is Stat stat &&
                   Min.Equals(stat.Min) &&
                   Max.Equals(stat.Max) &&
                   Avg.Equals(stat.Avg) &&
                   Mean.Equals(stat.Mean);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Min, Max, Avg, Mean);
        }

        public override string ToString()
        {
            return $"Avg:{Avg} Range:({Min}-{Max}) Mean:{Mean}";
        }

        public static Stat? Create(List<TimeSpan> list)
        {
            if (list.Count == 0)
                return null;
            
            var (min, max, avg) = (list[0], list[0], list[0]);
            for (int i = 1; i < list.Count; ++i)
            {
                if (list[i] < min)
                    min = list[i];
                if (list[i] > max)
                    max = list[i];
                avg += list[i];
            }
            list.Sort();
            var mean = (list.Count % 2) == 0 ?
                (list[list.Count / 2 - 1] + list[list.Count / 2]) / 2 :
                list[list.Count / 2];
            return new Stat(min, max, avg / list.Count, mean);
        }
    }
}