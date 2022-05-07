using System;
using System.IO;
using System.Text.Json;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// A converter that can convert the result into a <see cref="HttpDataSource" />.
    /// </summary>
    public class JsonDataConverterAttribute : DataConverterAttribute, Tools.IDataConverter
    {
        
        /// <summary>
        /// The converter to use to implement your custom conversion method.
        /// </summary>
        public interface ICustomJsonConverter
        {
            bool Convert(Utf8JsonWriter writer, object value);
        }

        /// <summary>
        /// The options that should be used for the default JSON conversion. This will
        /// be ignored if <see cref="CustomConverter" /> is set.
        /// </summary>
        public JsonSerializerOptions? Options { get; set; }

        /// <summary>
        /// The writer options that is used to format the JSON data.
        /// </summary>
        public JsonWriterOptions WriterOptions { get; set; }

        /// <summary>
        /// The custom converter that is used to transform the JSON data to the desired format. <br/>
        /// This type is expected to implement <see cref="JsonDataConverterAttribute.ICustomJsonConverter" />.
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

        public Func<object, HttpDataSource?>? GetConverter(Type data)
        {
            Func<Utf8JsonWriter, object, bool>? writer = null;

            if (CustomConverter != null)
            {
                ICustomJsonConverter conv;
                try { conv = (ICustomJsonConverter)Activator.CreateInstance(CustomConverter); }
                catch { return null; }
                writer = conv.Convert;
            }
            else
            {
                writer = (w, value) =>
                {
                    try { JsonSerializer.Serialize(w, value, Options); }
                    catch { return false; }
                    return true;
                };
            }

            if (writer == null)
                return null;
            
            return value =>
            {
                var m = new MemoryStream();
                var w = new Utf8JsonWriter(m, WriterOptions);

                if (!writer(w, value))
                {
                    w.Dispose();
                    m.Dispose();
                    return null;
                }

                w.Flush();

                return new HttpStreamDataSource(m)
                {
                    MimeType = MimeType.ApplicationJson,
                };
            };
        }
    }
}