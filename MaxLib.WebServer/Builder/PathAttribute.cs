using System;
using System.Collections.Generic;
using System.Text;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Limits the call to a specific URL path. You can also assign variables here.
    /// </summary>
    public sealed class PathAttribute : Tools.RuleAttributeBase
    {

        private readonly List<(string, bool)> parts = new List<(string, bool)>();

        /// <summary>
        /// If true this will check if the URL path starts with the given string. If not this will
        /// check if the whole URL matches
        /// </summary>
        public bool Prefix { get; set; }

        /// <summary>
        /// Set the mode for the string comparison check. Default is
        /// <see cref="StringComparison.InvariantCultureIgnoreCase" />.
        /// </summary>
        public StringComparison StringComparison { get; set; }
            = StringComparison.InvariantCultureIgnoreCase;

        /// <summary>
        /// Limit the call to a specific URL path. You can set variables if wrap them with curly
        /// braces. <br/>
        /// <c>"/path/to/file"</c> will match if the url is <c>/path/to/file</c>.<br/>
        /// <c>"/path/{foo}/{bar}"</c> will match if the url starts with <c>path</c> and has two
        /// positional parameter. These will be assigned to <c>foo</c> and <c>bar</c>.
        /// </summary>
        /// <param name="path">the path string</param>
        public PathAttribute(string path)
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.StartsWith('{') && part.EndsWith('}'))
                    this.parts.Add((part.Substring(1, part.Length - 2), true));
                else this.parts.Add((part, false));
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Path: ");
            foreach (var (part, mode) in parts)
            {
                if (mode)
                    sb.AppendFormat("/{{{0}}}", part);
                else sb.AppendFormat("/{0}", part);
            }
            if (Prefix)
                sb.Append("/*");
            return sb.ToString();
        }

        public override bool CanWorkWith(WebProgressTask task, Dictionary<string, object?> vars)
        {
            var url = task.Request.Location.DocumentPathTiles;
            for (int i = 0; i < parts.Count && i < url.Length; ++i)
            {
                var (match, isVar) = parts[i];
                if (isVar)
                {
                    vars[match] = url[i];
                }
                else
                {
                    if (!string.Equals(match, url[i], StringComparison))
                        return false;
                }
            }
            return Prefix || url.Length == parts.Count;
        }
    }
}