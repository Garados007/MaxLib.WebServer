using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Limits the call to a specific URL path. You can also assign variables here.<br/>
    /// When two methods on the same type have overlapping paths, <see cref="Tools.Generator"
    /// /> orders them by specificity before falling back to <see cref="PriorityAttribute" />:
    /// an exact match outranks a <see cref="Prefix" /> match, and among two exact (or two
    /// prefix) matches, more literal segments and fewer <c>{var}</c> segments outrank fewer
    /// literal segments/more variables. If two overlapping routes tie on both specificity and
    /// priority, which one wins is not guaranteed - add an explicit
    /// <see cref="PriorityAttribute" /> to make the outcome deterministic.
    /// </summary>
    public sealed class PathAttribute : Tools.RuleAttributeBase, Debugger.IExplainableRule
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
            ArgumentNullException.ThrowIfNull(path);
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.StartsWith('{') && part.EndsWith('}'))
                    this.parts.Add((part[1..^1], true));
                else this.parts.Add((part, false));
            }
        }

        /// <summary>
        /// Used by <see cref="Tools.Generator" /> to order overlapping routes that share the
        /// same <see cref="WebServicePriority" />: higher wins. See the class doc comment for
        /// the exact ordering rules.
        /// </summary>
        internal int Specificity
        {
            get
            {
                var literalCount = 0;
                var varCount = 0;
                foreach (var (_, isVar) in parts)
                {
                    if (isVar)
                        ++varCount;
                    else
                        ++literalCount;
                }
                var score = literalCount * 1000 - varCount;
                return Prefix ? score : score + 1_000_000;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Path: ");
            foreach (var (part, mode) in parts)
            {
                if (mode)
                    sb.AppendFormat(CultureInfo.InvariantCulture, "/{{{0}}}", part);
                else sb.AppendFormat(CultureInfo.InvariantCulture, "/{0}", part);
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

        bool Debugger.IExplainableRule.CanWorkWith(WebProgressTask task, Dictionary<string, object?> vars, out string? reason)
        {
            var url = task.Request.Location.DocumentPathTiles;
            for (int i = 0; i < parts.Count && i < url.Length; ++i)
            {
                var (match, isVar) = parts[i];
                if (isVar)
                {
                    vars[match] = url[i];
                }
                else if (!string.Equals(match, url[i], StringComparison))
                {
                    reason = $"URL segment {i} was '{url[i]}', expected '{match}'";
                    return false;
                }
            }
            if (!Prefix && url.Length != parts.Count)
            {
                reason = $"the URL has {url.Length} segment(s), expected exactly {parts.Count}";
                return false;
            }
            reason = null;
            return true;
        }
    }
}