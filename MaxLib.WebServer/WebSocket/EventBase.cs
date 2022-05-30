using System;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    /// <summary>
    /// The base class for all events that can be sent or received from a WebSocket
    /// </summary>
    public abstract class EventBase
    {
        /// <summary>
        /// The name of the event to identify
        /// </summary>
        [JsonIgnore]
        public virtual string TypeName => GetType().Name;

        /// <summary>
        /// Tells <see cref="EventFactory" /> if a new instance is generated during the
        /// deserialization of the json data. If this property is false the current instance can be
        /// updated.
        /// </summary>
        [JsonIgnore]
        protected internal virtual bool DeserializeNew => false;

        /// <summary>
        /// Write the content as a JSON to <see cref="Utf8JsonWriter"/>
        /// </summary>
        /// <param name="writer">the <see cref="Utf8JsonWriter"/> to write into</param>
        public virtual void WriteJson(Utf8JsonWriter writer)
        {
            _ = writer ?? throw new ArgumentNullException(nameof(writer));
            writer.WriteStartObject();
            writer.WriteString("$type", TypeName);
            WriteJsonContent(writer);
            writer.WriteEndObject();
        }

        /// <summary>
        /// Write the value content of this event into the <see cref="Utf8JsonWriter"/>
        /// </summary>
        /// <param name="writer">the <see cref="Utf8JsonWriter"/> to write into</param>
        protected abstract void WriteJsonContent(Utf8JsonWriter writer);

        /// <summary>
        /// Reads the value content from JSON and returns the object with updated information. If
        /// <see cref="DeserializeNew" /> is false this will return the current object.
        /// </summary>
        /// <param name="json">the <see cref="JsonElement" /> to read from</param>
        /// <returns>the updated object</returns>
        public virtual EventBase? ReadJson(JsonElement json)
        {
            try { ReadJsonContent(json); }
            catch (JsonException e)
            {
                WebServerLog.Add(ServerLogType.Error, GetType(), "read json", "error: {0}", e);
                return null;
            }
            return this;
        }

        /// <summary>
        /// Read the value content from JSON
        /// </summary>
        /// <param name="json">the <see cref="JsonElement"/> to read from</param>
        public abstract void ReadJsonContent(JsonElement json);

        public virtual Frame? ToFrame()
        {
            using var m = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(m);
            try { WriteJson(writer); }
            catch (JsonException e)
            {
                WebServerLog.Add(ServerLogType.Error, GetType(), "write json", "error: {0}", e);
                return null;
            }
            writer.Flush();

            var frame = new Frame
            {
                OpCode = OpCode.Text,
                Payload = m.ToArray(),
                FinalFrame = true,
            };

            return frame;
        }
    }
}
