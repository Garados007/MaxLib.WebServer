using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Benchmark.Profiles
{
    public class ProfileId<T>
    {
        public ProfileId(T key, int increment)
        {
            Key = key;
            Increment = increment;
        }

        public T Key { get; }

        public int Increment { get; }

        public override bool Equals(object? obj)
        {
            return obj is ProfileId<T> id &&
                   EqualityComparer<T>.Default.Equals(Key, id.Key) &&
                   Increment == id.Increment;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key, Increment);
        }

        public override string ToString()
        {
            return $"{Key}#{Increment}";
        }

        public static bool operator ==(ProfileId<T> left, ProfileId<T> right)
            => Equals(left, right);
        
        public static bool operator !=(ProfileId<T> left, ProfileId<T> right)
            => !Equals(left, right);
    }
}