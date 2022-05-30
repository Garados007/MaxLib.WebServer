using System.Text.Json;

namespace MaxLib.WebServer.Builder.Converter
{
    /// <summary>
    /// The converter to use to implement your custom conversion method.
    /// </summary>
    public interface ICustomJsonDataConverter
    {
        bool Convert(Utf8JsonWriter writer, object value);
    }
}