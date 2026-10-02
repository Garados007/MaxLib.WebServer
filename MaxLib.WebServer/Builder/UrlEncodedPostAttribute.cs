using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            ArgumentNullException.ThrowIfNull(task);
            var post = task.Request.Post.Data;
            if (!(post is Post.UrlEncodedData data))
                return new Result<object?>();
            var key = Name ?? field;
            if (data.Parameter.TryGetValue(key, out string? value))
                return new Result<object?>(value);
            // above UrlEncodedData.MaximumCacheSize every field lives in Overflow and Parameter stays empty
            var entry = data.Overflow?.Entries
                .OfType<Post.MultipartFormData.FormData>()
                .FirstOrDefault(e => e.Name == key);
            if (entry == null)
                return new Result<object?>();
            if (entry.Content is ReadOnlyMemory<byte> content)
                return new Result<object?>(Encoding.UTF8.GetString(content.Span));
            if (entry.TempFile is FileInfo tempFile)
                return new Result<object?>(File.ReadAllText(tempFile.FullName, Encoding.UTF8));
            return new Result<object?>("");
        }

        public override string ToString() => Name != null ? $"UrlEncodedPost: {Name}" : "UrlEncodedPost";
    }
}