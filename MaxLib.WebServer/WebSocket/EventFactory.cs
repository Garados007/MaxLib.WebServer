using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    public class EventFactory
    {
        readonly Dictionary<string, Func<EventBase>> registry
            = new Dictionary<string, Func<EventBase>>();

        public void Add<T>()
            where T : EventBase, new()
            => Add<T>(new T().TypeName);

        public void Add<T>(string key)
            where T : EventBase, new()
            => registry.Add(key ?? throw new ArgumentNullException(nameof(key)), () => new T());

        public void Add(string key, Type type)
        {
            _ = key ?? throw new ArgumentNullException(nameof(key));
            _ = type ?? throw new ArgumentNullException(nameof(type));
            if (!type.IsSubclassOf(typeof(EventBase)))
                throw new ArgumentException("invalid type", nameof(type));
            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
                throw new ArgumentException("type has no parameterless constructor", nameof(type));
            registry.Add(key, () => (EventBase)constructor.Invoke(Array.Empty<object>()));
        }

        public EventBase Parse(Frame frame)
        {
            _ = frame ?? throw new ArgumentNullException(nameof(frame));
            var doc = JsonDocument.Parse(frame.Payload);
            var key = doc.RootElement.GetProperty("$type").GetString();
            if (key == null || !registry.TryGetValue(key, out Func<EventBase>? caller))
                throw new KeyNotFoundException($"type {{{key}}} not registered");
            var @event = caller();
            return @event.ReadJson(doc.RootElement);
        }

        public bool TryParse(Frame frame, [NotNullWhen(true)] out EventBase? @event)
        {
            _ = frame ?? throw new ArgumentNullException(nameof(frame));
            try
            {
                @event = Parse(frame);
                return true;
            }
            catch (Exception e)
            {
                WebServerLog.Add(new ServerLogItem(ServerLogType.Error, GetType(), "parse error", e.ToString()));
                @event = null;
                return false;
            }
        }
    }
}
