using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace MaxLib.WebServer.Sessions
{
    public class Session : IDictionary<string, object>
    {
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;

        public object this[string key] 
        { 
            get => Data[key]; 
            set => Data[key] = value; 
        }

        public Dictionary<string, object> Data { get; protected set; }
            = new Dictionary<string, object>();

        public ICollection<string> Keys 
            => Data.Keys;

        public ICollection<object> Values 
            => Data.Values;

        public int Count 
            => Data.Count;

        bool ICollection<KeyValuePair<string, object>>.IsReadOnly => false;

        public void Add(string key, object value)
            => Data.Add(key, value);

        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
            => Data.Add(item.Key, item.Value);

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
            => Data.Remove(key);

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