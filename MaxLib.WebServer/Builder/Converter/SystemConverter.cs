using System;

namespace MaxLib.WebServer.Builder.Converter
{
    public class SystemConverter : Tools.IConverter
    {
        public Func<object?, object?>? GetConverter(Type source, Type target)
        {
            // simple conversion
            if (source == target || target.IsAssignableFrom(source))
                return value => value;
            
            // check if IConvertible is implemented
            var iConvertible = typeof(IConvertible);
            if (iConvertible.IsAssignableFrom(source) && iConvertible.IsAssignableFrom(target))
                return value => Convert.ChangeType(value, target);
            
            // unknown conversion
            return null;
        }
    }
}