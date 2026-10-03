using System;
using System.IO;
using System.Text.Json;
using MaxLib.WebServer.Builder.Converter;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Interpret the source value as JSON and convert it to the target property value.
    /// </summary>
    public class JsonConverterAttribute : ConverterAttribute, Tools.IConverter
    {

        /// <summary>
        /// The options that should be used for the default JSON conversion. This will
        /// be ignored if <see cref="CustomConverter" /> is set.<br/>
        /// This type is expected to implement <see cref="IJsonSerializerOptions" />.
        /// </summary>
        public Type? Options { get; set; }

        /// <summary>
        /// The custom converter that is used to transform the JSON data to the desired format. <br/>
        /// This type is expected to implement <see cref="ICustomJsonConverter" />.
        /// </summary>
        public Type? CustomConverter { get; set; }

        /// <summary>
        /// Create a new converter that can convert JSON data into the property value
        /// </summary>
        public JsonConverterAttribute()
            : base(typeof(JsonConverterAttribute), false)
        {
            Instance = this;
        }

        public override string ToString() =>
            CustomConverter != null ? $"JsonConverter: {CustomConverter.Name}" : "JsonConverter";

        // The JsonDocument backing the parsed element, if this method created it (string/Stream input).
        // A JsonDocument/JsonElement passed in stays owned by the caller and is not disposed here.
        private static (JsonElement Element, JsonDocument? Owned)? PreParse(object? x, Type source)
        {
            if (x is null)
                return null;
            if (typeof(string).IsAssignableFrom(source))
            {
                var doc = JsonDocument.Parse((string)x);
                return (doc.RootElement, doc);
            }
            if (typeof(Stream).IsAssignableFrom(source))
            {
                var doc = JsonDocument.Parse((Stream)x);
                return (doc.RootElement, doc);
            }
            if (typeof(JsonDocument).IsAssignableFrom(source))
                return (((JsonDocument)x).RootElement, null);
            if (typeof(JsonElement).IsAssignableFrom(source))
                return ((JsonElement)x, null);
            return null;
        }

        public Func<object?, object?>? GetConverter(Type source, Type target)
        {
            if (!typeof(string).IsAssignableFrom(source) && !typeof(Stream).IsAssignableFrom(source)
                && !typeof(JsonDocument).IsAssignableFrom(source) && !typeof(JsonElement).IsAssignableFrom(source))
                return null;

            if (CustomConverter != null)
            {
                ICustomJsonConverter conv;
                try { conv = (ICustomJsonConverter)Activator.CreateInstance(CustomConverter)!; }
                catch { return null; }

                return x =>
                {
                    var res = PreParse(x, source);
                    if (res is null)
                        return null;
                    using var owned = res.Value.Owned;
                    // Clone the result: a converter may return the JsonElement itself, which becomes invalid
                    // once `owned` is disposed below.
                    var element = owned != null ? res.Value.Element.Clone() : res.Value.Element;
                    return conv.Convert(element, target);
                };
            }
            else
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                };
                if (Options != null && typeof(IJsonSerializerOptions).IsAssignableFrom(Options))
                {
                    var constructor = Options.GetConstructor(Type.EmptyTypes);
                    if (constructor != null)
                        options = ((IJsonSerializerOptions)constructor.Invoke([])).Options;
                }
                return x =>
                {
                    var res = PreParse(x, source);
                    if (res is null)
                        return null;
                    using var owned = res.Value.Owned;
                    return res.Value.Element.Deserialize(target, options);
                };
            }
        }
    }
}