using System;

namespace MaxLib.WebServer.Builder.Tools
{
    public interface IDataConverter
    {
        Func<object, HttpDataSource?>? GetConverter(Type data);
    }
}