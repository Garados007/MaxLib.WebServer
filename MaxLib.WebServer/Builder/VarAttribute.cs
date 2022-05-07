using System;
using System.Collections.Generic;
using MaxLib.WebServer.Builder.Tools;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Specify this parameter should use a variable as its value source.
    /// </summary>
    public sealed class VarAttribute : ParamAttributeBase
    {
        /// <summary>
        /// The name of the variable that should be used. If this field is null it will use the name
        /// of the parameter.
        /// </summary>
        public string? Name { get; set; }

        public override Type Type => typeof(string);

        /// <summary>
        /// Specify this parameter should use a variable as its value source. This will use the
        /// parameter name as the name of the variable.
        /// </summary>
        public VarAttribute()
        {
        }

        /// <summary>
        /// Specify this parameter should use a variable as its value source.
        /// </summary>
        /// <param name="name">The name of the variable</param>
        public VarAttribute(string name)
        {
            Name = name;
        }

        public override Result<object?> GetValue(WebProgressTask task, string field,
            Dictionary<string, object?> vars
        )
        {
            if (!vars.TryGetValue(Name ?? field, out object? value))
                return new Result<object?>();
            return new Result<object?>(value);
        }
    }
}