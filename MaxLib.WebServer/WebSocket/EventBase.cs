using System;
using System.Text.Json;

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
        public virtual string TypeName => GetType().Name;

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
        /// Read the value content from JSON
        /// </summary>
        /// <param name="json">the <see cref="JsonElement"/> to read from</param>
        public abstract void ReadJsonContent(JsonElement json);

        public virtual Frame ToFrame()
        {
            using var m = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(m);
            WriteJson(writer);
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
