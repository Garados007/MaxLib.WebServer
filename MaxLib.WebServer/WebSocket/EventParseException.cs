using System;
using System.Text.Json;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    /// <summary>
    /// Base class for the reasons <see cref="EventFactory.Parse(Frame)" /> can fail to turn an
    /// incoming <see cref="Frame" /> into an <see cref="EventBase" />. <see cref="EventConnection" />
    /// catches each concrete subclass separately and routes it to a matching overridable handler,
    /// so a subclass can react to (and recover from) client-supplied garbage instead of the
    /// connection silently dropping the frame.
    /// </summary>
    [Serializable]
    public abstract class EventParseException : Exception
    {
        protected EventParseException()
        {
        }

        protected EventParseException(string message)
            : base(message)
        {
        }

        protected EventParseException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// The frame payload is not syntactically valid JSON.
    /// </summary>
    [Serializable]
    public sealed class MalformedEventJsonException : EventParseException
    {
        public MalformedEventJsonException()
        {
        }

        public MalformedEventJsonException(string message)
            : base(message)
        {
        }

        public MalformedEventJsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public MalformedEventJsonException(JsonException innerException)
            : base("The WebSocket frame payload is not valid JSON.",
                  innerException ?? throw new ArgumentNullException(nameof(innerException)))
        {
        }
    }

    /// <summary>
    /// The frame payload is valid JSON, but its "$type" discriminator is missing, not a string,
    /// or does not match any event type registered on the owning <see cref="EventFactory" />.
    /// </summary>
    [Serializable]
    public sealed class UnknownEventTypeException : EventParseException
    {
        /// <summary>
        /// The "$type" value found on the wire, or <see langword="null" /> if the payload had no
        /// usable "$type" discriminator at all.
        /// </summary>
        public string? EventTypeName { get; }

        public UnknownEventTypeException()
        {
        }

        public UnknownEventTypeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public UnknownEventTypeException(string? eventTypeName)
            : base(eventTypeName == null
                  ? "The event payload has no '$type' discriminator."
                  : $"No event type is registered for \"$type\" = \"{eventTypeName}\".")
        {
            EventTypeName = eventTypeName;
        }
    }

    /// <summary>
    /// The frame payload's "$type" discriminator matched a registered event type, but the
    /// payload could not be mapped onto that type (e.g. a property has the wrong shape).
    /// </summary>
    [Serializable]
    public sealed class InvalidEventPayloadException : EventParseException
    {
        /// <summary>
        /// The registered "$type" value the payload named, or <see langword="null" /> if this
        /// exception was constructed without one.
        /// </summary>
        public string? EventTypeName { get; }

        public InvalidEventPayloadException()
        {
        }

        public InvalidEventPayloadException(string message)
            : base(message)
        {
        }

        public InvalidEventPayloadException(string eventTypeName, Exception innerException)
            : base($"The event payload for \"$type\" = \"{eventTypeName}\" could not be deserialized.",
                  innerException ?? throw new ArgumentNullException(nameof(innerException)))
        {
            EventTypeName = eventTypeName ?? throw new ArgumentNullException(nameof(eventTypeName));
        }
    }
}
