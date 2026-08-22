using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    /// <summary>
    /// A registry of <see cref="EventBase" /> subclasses, keyed by their wire "$type" value, that
    /// builds a <see cref="JsonSerializerOptions" /> using native <see cref="System.Text.Json" />
    /// polymorphic serialization to (de)serialize <see cref="Frame" /> payloads. Register every
    /// event type via <see cref="Add{T}()" />/<see cref="Add(string, Type)" /> before the first
    /// call to <see cref="Parse(Frame)" />/<see cref="ToFrame(EventBase)" /> — the registry is
    /// sealed on first use and further registrations throw.
    /// </summary>
    public class EventFactory
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<EventFactory>();
        static readonly EventId ParseErrorEventId = new(0, "parse error");
        static readonly EventId WriteJsonEventId = new(0, "write json");

        readonly Dictionary<string, Type> registry = [];
        readonly Lock sealLock = new();
        readonly JsonSerializerOptions? seedOptions;
        JsonSerializerOptions? sealedOptions;

        public EventFactory()
            : this(null)
        {
        }

        /// <param name="seedOptions">
        /// A template <see cref="JsonSerializerOptions" /> to copy settings from (naming policy,
        /// custom converters, etc.) before this factory layers its own polymorphic type
        /// resolution on top. The instance you pass is never mutated or reused directly — its
        /// settings are copied.
        /// </param>
        public EventFactory(JsonSerializerOptions? seedOptions)
        {
            this.seedOptions = seedOptions;
        }

        public void Add<T>()
            where T : EventBase, new()
            => Add<T>(new T().TypeName);

        public void Add<T>(string key)
            where T : EventBase, new()
            => AddCore(key, typeof(T));

        public void Add(string key, Type type)
        {
            _ = type ?? throw new ArgumentNullException(nameof(type));
            if (!type.IsSubclassOf(typeof(EventBase)))
                throw new ArgumentException("invalid type", nameof(type));
            if (type.GetConstructor(Type.EmptyTypes) == null)
                throw new ArgumentException("type has no parameterless constructor", nameof(type));
            AddCore(key, type);
        }

        void AddCore(string key, Type type)
        {
            _ = key ?? throw new ArgumentNullException(nameof(key));
            lock (sealLock)
            {
                if (sealedOptions != null)
                    throw new InvalidOperationException(
                        "This EventFactory has already been used to serialize or deserialize " +
                        "an event; its type registry is sealed. Register all event types before " +
                        $"first use. (Attempted to add '{key}' -> {type}.)");
                registry.Add(key, type);
            }
        }

        /// <summary>
        /// The <see cref="JsonSerializerOptions" /> used for all serialization and
        /// deserialization operations of this factory. Lazily built (and sealed) on first
        /// access.
        /// </summary>
        public JsonSerializerOptions Options
        {
            get
            {
                if (sealedOptions != null)
                    return sealedOptions;
                lock (sealLock)
                {
                    return sealedOptions ??= BuildOptions();
                }
            }
        }

        JsonSerializerOptions BuildOptions()
        {
            var snapshot = registry.ToArray();
            // Copy (never reuse-in-place) the seed so the caller's own options object is
            // untouched and can still be used/locked elsewhere.
            var options = seedOptions != null
                ? new JsonSerializerOptions(seedOptions)
                : new JsonSerializerOptions();
            IJsonTypeInfoResolver baseResolver = options.TypeInfoResolver
                ?? new DefaultJsonTypeInfoResolver();
            options.TypeInfoResolver = baseResolver.WithAddedModifier(typeInfo =>
            {
                if (typeInfo.Type != typeof(EventBase))
                    return;
                var poly = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "$type",
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                };
                foreach (var (key, type) in snapshot)
                    poly.DerivedTypes.Add(new JsonDerivedType(type, key));
                typeInfo.PolymorphismOptions = poly;
            });
            return options;
        }

        public EventBase? Parse(Frame frame)
        {
            _ = frame ?? throw new ArgumentNullException(nameof(frame));
            return JsonSerializer.Deserialize<EventBase>(frame.Payload.Span, Options);
        }

        public bool TryParse(Frame frame, [NotNullWhen(true)] out EventBase? @event)
        {
            _ = frame ?? throw new ArgumentNullException(nameof(frame));
            try
            {
                @event = Parse(frame);
                return @event != null;
            }
            catch (Exception e)
            {
                logger.LogError(ParseErrorEventId, e, "Error parsing WebSocket frame");
                @event = null;
                return false;
            }
        }

        /// <summary>
        /// Serializes an event into a <see cref="Frame" /> using this factory's
        /// <see cref="Options" />.
        /// </summary>
        public Frame? ToFrame(EventBase @event)
        {
            _ = @event ?? throw new ArgumentNullException(nameof(@event));
            try
            {
                using var m = new MemoryStream();
                using (var writer = new Utf8JsonWriter(m))
                    JsonSerializer.Serialize(writer, @event, typeof(EventBase), Options);
                return new Frame
                {
                    OpCode = OpCode.Text,
                    Payload = m.ToArray(),
                    FinalFrame = true,
                };
            }
            catch (JsonException e)
            {
                logger.LogError(WriteJsonEventId, e, "Error writing JSON content");
                return null;
            }
        }
    }
}
