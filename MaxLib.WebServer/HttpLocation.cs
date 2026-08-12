using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public partial class HttpLocation
    {
        public string Url { get; private set; }

        public string DocumentPath { get; private set; }

        public string[] DocumentPathTiles { get; private set; }

        public string CompleteGet { get; private set; }

        public Dictionary<string, string> GetParameter { get; }

        public virtual void SetLocation(string url)
        {
            Url = url ?? throw new ArgumentNullException(url);
                GetParameter.Clear();
            var match = UrlRegex().Match(url);
            if (!match.Success)
            {
                DocumentPath = url;
                DocumentPathTiles = [url];
                CompleteGet = "";
            }
            DocumentPath = match.Groups[1].Value;
            DocumentPathTiles = match.Groups[2].Captures
                .OfType<Capture>()
                .Select(c => WebServerUtils.DecodeUri(c.Value))
                .ToArray();
            CompleteGet = match.Groups[3].Success ? match.Groups[3].Value ?? "" : "";
            foreach (Capture capture in match.Groups[4].Captures)
            {
                var submatch = ArgsRegex().Match(capture.Value);
                if (submatch.Success)
                {
                    GetParameter[WebServerUtils.DecodeUri(submatch.Groups[1].Value)]
                        = WebServerUtils.DecodeUri(submatch.Groups[2].Value);
                }
                else
                {
                    GetParameter[capture.Value] = "";
                }
            }
        }

        public HttpLocation(string url)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            GetParameter = new Dictionary<string, string>();
            DocumentPath = "";
            DocumentPathTiles = [];
            CompleteGet = "";
            SetLocation(url);
        }

        public override string ToString()
        {
            return Url;
        }

        public bool IsUrl(string[] urlTiles, bool ignoreCase = false)
        {
            ArgumentNullException.ThrowIfNull(urlTiles);
            if (urlTiles.Length != DocumentPathTiles.Length) return false;
            for (int i = 0; i < urlTiles.Length; ++i)
                if (ignoreCase)
                {
                    if (!string.Equals(urlTiles[i], DocumentPathTiles[i], StringComparison.OrdinalIgnoreCase)) return false;
                }
                else
                {
                    if (urlTiles[i] != DocumentPathTiles[i]) return false;
                }
            return true;
        }

        public bool StartsUrlWith(string[] urlTiles, bool ignoreCase = false)
        {
            ArgumentNullException.ThrowIfNull(urlTiles);
            if (urlTiles.Length > DocumentPathTiles.Length) return false;
            for (int i = 0; i < urlTiles.Length; ++i)
                if (ignoreCase)
                {
                    if (!string.Equals(urlTiles[i], DocumentPathTiles[i], StringComparison.OrdinalIgnoreCase)) return false;
                }
                else
                {
                    if (urlTiles[i] != DocumentPathTiles[i]) return false;
                }
            return true;
        }

        [GeneratedRegex(@"^((?:\/+([^\/?]+))*\/?)(?:\?((?:([^&$]*)&?)*))?$")]
        private static partial Regex UrlRegex();
        [GeneratedRegex(@"^([^=]*)=(.*)$")]
        private static partial Regex ArgsRegex();
    }
}
