using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace MaxLib.WebServer.Sessions
{
    public class Session : IDictionary<string, object>
    {
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The key this session is currently stored under. Maintained by the session service; do not set it directly.
        /// </summary>
        public string Key { get; internal set; } = "";

        public object this[string key]
        {
            get => Data[key];
            set => Data[key] = value;
        }

        /// <summary>
        /// The session's data. Thread-safe, so concurrent requests sharing a session id can use it at the same time.
        /// Use the atomic <see cref="ConcurrentDictionary{TKey, TValue}" /> methods for read-modify-write sequences.
        /// </summary>
        public ConcurrentDictionary<string, object> Data { get; protected set; }
            = new ConcurrentDictionary<string, object>();

        public ICollection<string> Keys
            => Data.Keys;

        public ICollection<object> Values
            => Data.Values;

        public int Count
            => Data.Count;

        bool ICollection<KeyValuePair<string, object>>.IsReadOnly => false;

        public void Add(string key, object value)
            => ((IDictionary<string, object>)Data).Add(key, value);

        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
            => ((IDictionary<string, object>)Data).Add(item.Key, item.Value);

        public void Clear()
            => Data.Clear();

        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
        {
            return ((ICollection<KeyValuePair<string, object>>)Data).Contains(item);
        }

        public bool ContainsKey(string key)
            => Data.ContainsKey(key);

        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, object>>)Data).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            => Data.GetEnumerator();

        public bool Remove(string key)
            => Data.TryRemove(key, out _);

        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
        {
            return ((ICollection<KeyValuePair<string, object>>)Data).Remove(item);
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
        {
            return Data.TryGetValue(key, out value);
        }

        bool IDictionary<string, object>.TryGetValue(string key, out object value)
            => TryGetValue(key, out value!);

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
        {
            if (TryGetValue(key, out object? rawValue) && rawValue is T realValue)
            {
                value = realValue;
                return true;
            }
            else
            {
                value = default!; // dirty
                return false;
            }
        }

        public T Get<T>(string key)
        {
            if (Data[key] is T value)
                return value;
            else throw new KeyNotFoundException($"value from {key} cannot be transformed to {typeof(T)}");
        }
    }
}
