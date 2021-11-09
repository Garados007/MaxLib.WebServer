using System.Text.RegularExpressions;
using System.Text;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Post
{
    public class UrlEncodedData : IPostData
    {
        public string MimeType => WebServer.MimeType.ApplicationXWwwFromUrlencoded;

        public Dictionary<string, string> Parameter { get; }
            = new Dictionary<string, string>();

        public void Set(string content, string options)
        {
            _ = content ?? throw new ArgumentNullException(nameof(content));
            Parameter.Clear();
            if (content != "")
            {
                var tiles = content.Split('&');
                foreach (var tile in tiles)
                {
                    var ind = tile.IndexOf('=');
                    if (ind == -1)
                    {
                        var t = WebServerUtils.DecodeUri(tile);
                        if (!Parameter.ContainsKey(t)) 
                            Parameter.Add(t, "");
                    }
                    else
                    {
                        var key = WebServerUtils.DecodeUri(tile.Remove(ind));
                        var value = ind + 1 == tile.Length ? "" : tile.Substring(ind + 1);
                        if (!Parameter.ContainsKey(key)) 
                            Parameter.Add(key, WebServerUtils.DecodeUri(value));
                    }
                }
            }
        }

        static readonly Regex charsetRegex = new Regex(
            "charset\\s*=\\s*(?<charset>[^\\s;]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        public async Task SetAsync(WebProgressTask task, IO.ContentStream content, string options)
        {
            var match = charsetRegex.Match(options);
            Encoding? encoding = null;
            if (match.Success)
                try
                { 
                    encoding = Encoding.GetEncoding(match.Groups["charset"].Value);
                }
                catch (Exception e)
                {
                    WebServerLog.Add(ServerLogType.Error, GetType(), "SetPost", 
                        $"invalid encoding {match.Groups["charset"].Value}: {e}");
                }
            encoding ??= Encoding.UTF8;
            var buffer = new byte[content.UnreadData];
            await content.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            Set(encoding.GetString(buffer), options);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var (key, value) in Parameter)
                sb.AppendLine($"{key}: {value}");
            return sb.ToString();
        }

        public void Dispose()
        {
        }
    }
}