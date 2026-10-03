using System.Text.Json.Serialization;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    /// <summary>
    /// The base class for all events that can be sent or received from a WebSocket. Subclasses
    /// are plain classes with public properties, serialized and deserialized by
    /// <see cref="System.Text.Json.JsonSerializer" /> via the polymorphic type resolution built
    /// by the owning <see cref="EventFactory" /> — see <see cref="EventFactory.ToFrame(EventBase)" />
    /// and <see cref="EventFactory.Parse(Frame)" />.
    /// </summary>
    public abstract class EventBase
    {
        /// <summary>
        /// The default "$type" key used by <see cref="EventFactory.Add{T}()" />. Only consulted
        /// at registration time — once registered, the wire discriminator is whatever key was
        /// passed to <c>Add</c>, independent of this property.
        /// </summary>
        [JsonIgnore]
        public virtual string TypeName => GetType().Name;
    }
}
