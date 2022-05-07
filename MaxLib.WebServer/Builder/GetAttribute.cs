using System;
using System.Collections.Generic;
using MaxLib.WebServer.Builder.Tools;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Marks a parameter that this should take its value from the GET values of the requests.
    /// </summary>
    public sealed class GetAttribute : ParamAttributeBase
    {
        public string? Name { get; set; }

        public override Type Type => typeof(string);

        /// <summary>
        /// This paramter should use the value from the GET parameter with the same name
        /// </summary>
        public GetAttribute()
        {
        }

        /// <summary>
        /// This paramter should use the value from the GET parameter with the name
        /// <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the GET parameter</param>
        public GetAttribute(string name)
        {
            Name = name;
        }

        public override Result<object?> GetValue(WebProgressTask task, string field,
            Dictionary<string, object?> vars
        )
        {
            if (!task.Request.Location.GetParameter.TryGetValue(Name ?? field, out string value))
                return new Result<object?>();
            return new Result<object?>(value);
        }
    }
}