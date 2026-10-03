using System;
using System.IO;
using System.Text.Json;
using MaxLib.WebServer.Builder.Converter;
using Microsoft.Extensions.Logging;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// A converter that can convert the result into a <see cref="HttpDataSource" />.
    /// </summary>
    public partial class JsonDataConverterAttribute : DataConverterAttribute, Tools.IDataConverter
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<JsonDataConverterAttribute>();
        static readonly EventId JsonConvertEventId = new(0, "JSON Convert");

        /// <summary>
        /// The options that should be used for the default JSON conversion. This will
        /// be ignored if <see cref="CustomConverter" /> is set.<br/>
        /// This type is expected to implement <see cref="IJsonSerializerOptions" />.
        /// </summary>
        public Type? Options { get; set; }

        /// <summary>
        /// The options that should be used for the <see cref="Utf8JsonWriter" />.<br/>
        /// This type is expected to implement <see cref="IJsonWriterOptions" />.
        /// </summary>
        public Type? JsonWriterOptions { get; set ;}

        /// <summary>
        /// The custom converter that is used to transform the JSON data to the desired format. <br/>
        /// This type is expected to implement <see cref="ICustomJsonConverter" />.
        /// </summary>
        public Type? CustomConverter { get; set; }

        /// <summary>
        /// Creates a new converter that can convert the result into a <see cref="HttpDataSource" />.
        /// </summary>
        public JsonDataConverterAttribute()
            : base(typeof(JsonDataConverterAttribute), false)
        {
            Instance = this;
        }

        /// <summary>
        /// Creates a new converter that can convert the result into a <see cref="HttpDataSource" />.
        /// </summary>
        public JsonDataConverterAttribute(Type customConverter)
            : base(typeof(JsonDataConverterAttribute), false)
        {
            Instance = this;
            CustomConverter = customConverter;
        }

        public override string ToString() =>
            CustomConverter != null ? $"JsonDataConverter: {CustomConverter.Name}" : "JsonDataConverter";

        public Func<object, HttpDataSource?>? GetConverter(Type data)
        {
            Func<Utf8JsonWriter, object, bool>? writer = null;

            if (CustomConverter != null)
            {
                ICustomJsonDataConverter conv;
                try { conv = (ICustomJsonDataConverter)Activator.CreateInstance(CustomConverter)!; }
                catch (Exception e)
                {
                    logger.LogError(JsonConvertEventId, e, "Error creating custom JSON converter instance");
                    return null;
                }
                writer = conv.Convert;
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
                writer = (w, value) =>
                {
                    if (value == null)
                    {
                        w.WriteNullValue();
                        return true;
                    }
                    try { JsonSerializer.Serialize(w, value, options); }
                    catch { return false; }
                    return true;
                };
            }

            if (writer == null)
                return null;

            var writerOptions = new JsonWriterOptions
            {
                Indented = true,
            };
            if (JsonWriterOptions != null && typeof(IJsonWriterOptions).IsAssignableFrom(JsonWriterOptions))
            {
                var constructor = JsonWriterOptions.GetConstructor(Type.EmptyTypes);
                if (constructor != null)
                    writerOptions = ((IJsonWriterOptions)constructor.Invoke([])).Options;
            }

            return value =>
            {
                var m = new MemoryStream();
                var w = new Utf8JsonWriter(m, writerOptions);

                if (!writer(w, value))
                {
                    w.Dispose();
                    m.Dispose();
                    return null;
                }

                w.Flush();
                w.Dispose();

                return new HttpStreamDataSource(m)
                {
                    MimeType = MimeType.ApplicationJson,
                };
            };
        }
    }
}
