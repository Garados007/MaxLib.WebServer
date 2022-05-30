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

        public Func<object?, object?>? GetConverter(Type source, Type target)
        {
            Func<object?, JsonElement?>? preParse = null;

            if (typeof(string).IsAssignableFrom(source))
                preParse = x =>
                {
                    if (x is null)
                        return null;
                    try { return JsonDocument.Parse((string)x).RootElement; }
                    catch { return null; }
                };
            if (typeof(Stream).IsAssignableFrom(source))
                preParse = x =>
                {
                    if (x is null)
                        return null;
                    try { return JsonDocument.Parse((Stream)x).RootElement; }
                    catch { return null; }
                };
            if (typeof(JsonDocument).IsAssignableFrom(source))
                preParse = x =>
                {
                    if (x is null)
                        return null;
                    return ((JsonDocument)x).RootElement;
                };
            if (typeof(JsonElement).IsAssignableFrom(source))
                preParse = x =>
                {
                    if (x is null)
                        return null;
                    return (JsonElement)x;
                };

            if (preParse is null)
                return null;
            
            if (CustomConverter != null)
            {
                ICustomJsonConverter conv;
                try { conv = (ICustomJsonConverter)Activator.CreateInstance(CustomConverter); }
                catch { return null; }

                return x =>
                {
                    var res = preParse(x);
                    if (res is null)
                        return null;
                    else return conv.Convert(res.Value, target);
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
                        options = ((IJsonSerializerOptions)constructor.Invoke(Array.Empty<object>())).Options;
                }
                return x => preParse(x)?.Deserialize(target, options);
            }
        }
    }
}