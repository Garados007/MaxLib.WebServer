using System.Text.Json;

namespace MaxLib.WebServer.Builder.Converter
{
    public interface IJsonSerializerOptions
    {
        public JsonSerializerOptions Options { get; }
    }
}