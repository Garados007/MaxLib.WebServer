using System.Text.Json;

namespace MaxLib.WebServer.Builder.Converter
{
    public interface IJsonWriterOptions
    {
        JsonWriterOptions Options { get; }
    }
}