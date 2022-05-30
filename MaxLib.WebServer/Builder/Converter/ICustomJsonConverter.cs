using System;
using System.Text.Json;

namespace MaxLib.WebServer.Builder.Converter
{
    /// <summary>
    /// The converter to use to implement your custom conversion method.
    /// </summary>
    public interface ICustomJsonConverter
    {
        object Convert(JsonElement value, Type target);
    }
}