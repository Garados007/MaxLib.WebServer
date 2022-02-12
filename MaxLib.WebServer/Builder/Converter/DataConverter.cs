using System;

namespace MaxLib.WebServer.Builder.Converter
{
    public class DataConverter : Tools.IDataConverter
    {
        public Func<object, HttpDataSource>? GetConverter(Type data)
        {
            // check if inherited from HttpDataSource
            if (typeof(HttpDataSource).IsAssignableFrom(data))
                return value => (HttpDataSource)value;
            
            // check common other types
            if (typeof(string).IsAssignableFrom(data))
                return value => new HttpStringDataSource((string)value);
            if (typeof(System.IO.Stream).IsAssignableFrom(data))
                return value => new HttpStreamDataSource((System.IO.Stream)value);
            if (typeof(System.IO.FileInfo).IsAssignableFrom(data))
                return value => new HttpFileDataSource(((System.IO.FileInfo)value).FullName);
            
            // unknown return type
            return null;
        }
    }
}