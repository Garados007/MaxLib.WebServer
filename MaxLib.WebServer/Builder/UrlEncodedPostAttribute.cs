using System;
using System.Collections.Generic;
using MaxLib.WebServer.Builder.Tools;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Receive the data from an POST request with "application/x-www-form-urlencoded" body. This will
    /// search for a single key and provide the data.
    /// </summary>
    public class UrlEncodedPostAttribute : Tools.ParamAttributeBase
    {
        /// <summary>
        /// The Name to use to fetch the data from the POST request
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Receive the data from an POST request with "application/x-www-form-urlencoded" body. This will
        /// search for a single key and provide the data. <br/>
        /// As the key the name of the parameter will be used.
        /// </summary>
        public UrlEncodedPostAttribute() {}

        /// <summary>
        /// Receive the data from an POST request with "application/x-www-form-urlencoded" body. This will
        /// search for a single key and provide the data.
        /// </summary>
        /// <param name="name">The name of the key that is looked for.</param>
        public UrlEncodedPostAttribute(string name)
        {
            Name = name;
        }

        public override Type Type => typeof(string);

        public override Result<object?> GetValue(WebProgressTask task, string field, Dictionary<string, object?> vars)
        {
            var post = task.Request.Post.Data;
            if (!(post is Post.UrlEncodedData data))
                return new Result<object?>();
            if (!data.Parameter.TryGetValue(Name ?? field, out string value))
                return new Result<object?>();
            return new Result<object?>(value);
        }
    }
}