using System;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    /// <summary>
    /// The base class for all websocket events that are serialized and deserialized using
    /// <see cref="JsonSerializer" />.
    /// </summary>
    public abstract class SerializedEventBase : EventBase
    {
        /// <summary>
        /// The <see cref="JsonSerializerOptions" /> that are used for all serialization and
        /// deserialization operations.
        /// </summary>
        public static JsonSerializerOptions JsonSerializerOptions { get; set; }
            = new JsonSerializerOptions();

        /// <summary>
        /// Tells <see cref="EventFactory" /> if a new instance is generated during the
        /// deserialization of the json data. If this property is false the current instance can be
        /// updated.
        [JsonIgnore]
        protected internal override bool DeserializeNew => true;

        /// <summary>
        /// This tells the <see cref="JsonSerializer" /> the type name of this event. Do not use
        /// this property in your custom code. Any changes to this are ignored.
        /// </summary>
        [JsonPropertyName("$type")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string JsonTypeName
        {
            get => TypeName;
            set {}
        }

        /// <summary>
        /// Write the content as a JSON to <see cref="Utf8JsonWriter"/>
        /// </summary>
        /// <param name="writer">the <see cref="Utf8JsonWriter"/> to write into</param>
        public override void WriteJson(Utf8JsonWriter writer)
        {
            _ = writer ?? throw new ArgumentNullException(nameof(writer));
            JsonSerializer.Serialize(writer, this, JsonSerializerOptions);
        }

        /// <summary>
        /// Not supported
        /// </summary>
        /// <exception cref="NotSupportedException" />
        protected override void WriteJsonContent(Utf8JsonWriter writer)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Reads the value content from JSON and returns the object with updated information.
        /// </summary>
        /// <param name="json">the <see cref="JsonElement" /> to read from</param>
        /// <returns>the updated object</returns>
        public override EventBase ReadJson(JsonElement json)
        {
            return (EventBase)JsonSerializer.Deserialize(json, GetType(), JsonSerializerOptions)!;
        }

        /// <summary>
        /// Not supported
        /// </summary>
        /// <exception cref="NotSupportedException" />
        public override void ReadJsonContent(JsonElement json)
        {
            throw new NotSupportedException();
        }
    }
}