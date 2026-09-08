using System;
using System.Collections.Generic;
using System.Globalization;

namespace MaxLib.WebServer.Builder.Runtime
{
    public class Parameter : IParameter
    {
        public Tools.ParamAttributeBase ParamSource { get; }

        public Func<object?, object?> Converter { get; }

        public string Name { get; }

        /// <summary>
        /// The parameter type the converted value must end up as - i.e. the target method
        /// parameter's own type. Only used to build a descriptive message if <see
        /// cref="Converter" /> throws.
        /// </summary>
        public Type TargetType { get; }

        public Parameter(string name, Tools.ParamAttributeBase source, Func<object?, object?> converter,
            Type targetType
        )
        {
            Name = name;
            ParamSource = source;
            Converter = converter;
            TargetType = targetType;
        }

        public Tools.Result<object?> GetValue(WebProgressTask task, Dictionary<string, object?> vars)
        {
            var value = ParamSource.GetValue(task, Name, vars);
            try
            {
                return value.Map(Converter);
            }
            catch (Exception ex) when (ex is not HttpException)
            {
                var message = string.Format(CultureInfo.InvariantCulture,
                    "Parameter '{0}' has an invalid value: could not convert '{1}' to '{2}' ({3}: {4})",
                    Name, value.Value, TargetType, ex.GetType().Name, ex.Message
                );
                throw new HttpException(HttpStateCode.BadRequest, new HttpStringDataSource(message));
            }
        }
    }
}