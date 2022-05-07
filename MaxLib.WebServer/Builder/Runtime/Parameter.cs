using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Builder.Runtime
{
    public class Parameter : IParameter
    {
        public Tools.ParamAttributeBase ParamSource { get; }

        public Func<object?, object?> Converter { get; }

        public string Name { get; }

        public Parameter(string name, Tools.ParamAttributeBase source, Func<object?, object?> converter)
        {
            Name = name;
            ParamSource = source;
            Converter = converter;
        }

        public Tools.Result<object?> GetValue(WebProgressTask task, Dictionary<string, object?> vars)
        {
            var value = ParamSource.GetValue(task, Name, vars);
            return value.Map(Converter);
        }
    }
}