using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Services
{
    /// <summary>
    /// A mapper that can map resources from the local IO.
    /// </summary>
    public class LocalIOMapper : WebService
    {
        /// <summary>
        /// A mapping rule that can produce a document for the requested path
        /// </summary>
        public abstract class MappingRule
        {
            /// <summary>
            /// A rank. Higher numbers mean higher ranks. This is normaly the number
            /// of path segments.
            /// </summary>
            public abstract int Rank { get; }

            /// <summary>
            /// This maps the request to a document. The document is directly attached
            /// to the response. If this rule cannot map a request to a document then
            /// this method returns false
            /// </summary>
            /// <param name="path"></param>
            /// <param name="task">the current progress task</param>
            /// <returns>true if mapping was successful</returns>
            public abstract bool MapRequest(ReadOnlySpan<string> path, WebProgressTask task);
        }

        /// <summary>
        /// A mapping rule that maps to files on the local disk
        /// </summary>
        public class FileMappingRule : MappingRule
        {
            /// <summary>
            /// The url path
            /// </summary>
            public ReadOnlyMemory<string> UrlPath { get; }

            /// <summary>
            /// the local path on the local disk
            /// </summary>
            public string LocalBasePath { get; }

            /// <summary>
            /// Creates a new mapping rule to map files from disk
            /// </summary>
            /// <param name="urlPath">the url path</param>
            /// <param name="localBasePath">the path on the local disk</param>
            /// <exception cref="ArgumentNullException" />
            /// <exception cref="DirectoryNotFoundException" />
            public FileMappingRule(ReadOnlyMemory<string> urlPath, string localBasePath)
            {
                UrlPath = urlPath;
                LocalBasePath = localBasePath ?? throw new ArgumentNullException(nameof(localBasePath));
                if (!Directory.Exists(localBasePath))
                    throw new DirectoryNotFoundException($"local base path not found: {localBasePath}");
            }

            public override int Rank => UrlPath.Length;

            public override bool MapRequest(ReadOnlySpan<string> path, WebProgressTask task)
            {
                var loc = task.Request.Location;
                if (loc.DocumentPath.EndsWith('/') || path.Length < UrlPath.Length)
                    return false;
                for (int i = 0; i < UrlPath.Length; ++i)
                    if (UrlPath.Span[i] != path[i])
                        return false;
                Span<string> localPathTiles = new string[path.Length - UrlPath.Length + 1];
                localPathTiles[0] = LocalBasePath;
                path[UrlPath.Length..].CopyTo(localPathTiles[1..]);
                var localPath = Path.Combine(localPathTiles.ToArray());
                FileInfo fileInfo;
                try { fileInfo = new FileInfo(localPath); }
                catch (Exception e)
                {
                    WebServerLog.Add(ServerLogType.Error, GetType(), "map file", $"invalid path: {e}");
                    return false;
                }
                if (!fileInfo.Exists)
                    return false;
                var mime = MimeType.GetMimeTypeForExtension(fileInfo.Extension)
                        ?? MimeType.ApplicationOctetStream;
                if (task.Request.HeaderParameter.ContainsKey("Range"))
                {
                    var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var multipart = new MultipartRanges(stream, task, mime);
                    task.Document.DataSources.Add(multipart);
                }
                else
                {
                    task.Document.DataSources.Add(new HttpFileDataSource(localPath)
                    {
                        MimeType = mime,
                    });
                }
                return true;
            }
        }

        /// <summary>
        /// A mapping rule that maps to directories on the local disk
        /// </summary>
        public class DirectoryMappingRule : MappingRule
        {
            /// <summary>
            /// The url path
            /// </summary>
            public ReadOnlyMemory<string> UrlPath { get; }

            /// <summary>
            /// the local path on the local disk
            /// </summary>
            public string LocalBasePath { get; }

            /// <summary>
            /// Creates a new mapping rule to map directories from disk
            /// </summary>
            /// <param name="urlPath">the url path</param>
            /// <param name="localBasePath">the path on the local disk</param>
            /// <exception cref="ArgumentNullException" />
            /// <exception cref="DirectoryNotFoundException" />
            public DirectoryMappingRule(ReadOnlyMemory<string> urlPath, string localBasePath)
            {
                UrlPath = urlPath;
                LocalBasePath = localBasePath ?? throw new ArgumentNullException(nameof(localBasePath));
                if (!Directory.Exists(localBasePath))
                    throw new DirectoryNotFoundException($"local base path not found: {localBasePath}");
            }

            public override int Rank => UrlPath.Length;

            private ReadOnlySpan<string> EncodeUrl(ReadOnlySpan<string> value)
            {
                Span<string> result = new string[value.Length];
                for (int i = 0; i < value.Length; ++i)
                    result[i] = System.Net.WebUtility.UrlEncode(value[i]);
                return result;
            }

            public override bool MapRequest(ReadOnlySpan<string> path, WebProgressTask task)
            {
                var loc = task.Request.Location;
                if (path.Length < UrlPath.Length)
                    return false;
                for (int i = 0; i < UrlPath.Length; ++i)
                    if (UrlPath.Span[i] != path[i])
                        return false;
                Span<string> localPathTiles = new string[path.Length - UrlPath.Length + 1];
                localPathTiles[0] = LocalBasePath;
                path[UrlPath.Length..].CopyTo(localPathTiles[1..]);
                var localPath = Path.Combine(localPathTiles.ToArray());
                if (File.Exists(localPath) && !loc.DocumentPath.EndsWith('/'))
                    return false;
                DirectoryInfo directoryInfo;
                try { directoryInfo = new DirectoryInfo(localPath); }
                catch (Exception e)
                {
                    WebServerLog.Add(ServerLogType.Error, GetType(), "map file", $"invalid path: {e}");
                    return false;
                }
                if (!directoryInfo.Exists)
                    return false;

                var sb = new StringBuilder();
                sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/><title>");
                sb.Append(System.Net.WebUtility.HtmlEncode(directoryInfo.Name));
                sb.Append("</title><style>table{margin:0 0 1em 0;}table tr:nth-child(2n+1){background-color:lightgray;}</style></head><body><h1>");
                sb.Append(System.Net.WebUtility.HtmlEncode(string.Join('/', path.ToArray())));
                sb.Append("</h1><table style=\"width:100%;\">");
                if (path.Length > 0)
                {
                    sb.Append("<tr><td></td><td><a href=\"/");
                    sb.AppendJoin('/', EncodeUrl(path[..^1]).ToArray());
                    if (path.Length > 1)
                        sb.Append('/');
                    sb.Append("\">.. (move a layer up)</a></td><td></td></tr>");
                }
                foreach (var di in directoryInfo.GetDirectories())
                {
                    sb.Append("<tr><td>DIR</td><td><a href=\"/");
                    sb.AppendJoin('/', EncodeUrl(path).ToArray());
                    if (path.Length > 0)
                        sb.Append('/');
                    sb.Append(System.Net.WebUtility.UrlEncode(di.Name));
                    sb.Append("/\">");
                    sb.Append(System.Net.WebUtility.HtmlEncode(di.Name));
                    sb.Append("</a></td><td></td></tr>");
                }
                foreach (var fi in directoryInfo.GetFiles())
                {
                    sb.Append("<tr><td>FILE</td><td><a href=\"/");
                    sb.AppendJoin('/', EncodeUrl(path).ToArray());
                    if (path.Length > 0)
                        sb.Append('/');
                    sb.Append(System.Net.WebUtility.UrlEncode(fi.Name));
                    sb.Append("\">");
                    sb.Append(System.Net.WebUtility.HtmlEncode(fi.Name));
                    sb.Append("</a></td><td style=\"text-align:right\">");
                    sb.Append(WebServerUtils.GetVolumeString(fi.Length, true, 4));
                    sb.Append("</td></tr>");
                }
                sb.Append("</table><span>Creation date: ");
                sb.Append(WebServerUtils.GetDateString(DateTime.UtcNow));
                sb.Append("</span></body></html>");

                task.Document.DataSources.Add(new HttpStringDataSource(sb.ToString())
                {
                    MimeType = MimeType.TextHtml,
                });
                task.Response.StatusCode = HttpStateCode.OK;

                return true;
            }
        }

        private readonly List<MappingRule> mappingRules = new List<MappingRule>();

        /// <summary>
        /// Create a new local IO mapper that can map resources from the local io.
        /// </summary>
        public LocalIOMapper() 
            : base(ServerStage.CreateDocument)
        {
        }

        /// <summary>
        /// Add a new mapping rule
        /// </summary>
        /// <param name="rule">the rule to add</param>
        public void Add(MappingRule rule)
            => mappingRules.Add(rule ?? throw new ArgumentNullException(nameof(rule)));

        /// <summary>
        /// Add a new file mapping rule
        /// </summary>
        /// <param name="urlPath">the url path</param>
        /// <param name="localBasePath">the path on the local file system</param>
        public void AddFileMapping(ReadOnlyMemory<string> urlPath, string localBasePath)
            => Add(new FileMappingRule(urlPath, localBasePath));

        /// <summary>
        /// Add a new file mapping rule
        /// </summary>
        /// <param name="urlPath">the url path</param>
        /// <param name="localBasePath">the path on the local file system</param>
        public void AddFileMapping(string urlPath, string localBasePath)
        {
            _ = urlPath ?? throw new ArgumentNullException(nameof(urlPath));
            Add(new FileMappingRule(
                urlPath.Split('/', StringSplitOptions.RemoveEmptyEntries), 
                localBasePath
            ));
        }

        /// <summary>
        /// Add a new directory mapping rule
        /// </summary>
        /// <param name="urlPath">the url path</param>
        /// <param name="localBasePath">the path on the local file system</param>
        public void AddDirectoryMapping(ReadOnlyMemory<string> urlPath, string localBasePath)
            => Add(new DirectoryMappingRule(urlPath, localBasePath));

        /// <summary>
        /// Add a new directory mapping rule
        /// </summary>
        /// <param name="urlPath">the url path</param>
        /// <param name="localBasePath">the path on the local file system</param>
        public void AddDirectoryMapping(string urlPath, string localBasePath)
        {
            _ = urlPath ?? throw new ArgumentNullException(nameof(urlPath));
            Add(new DirectoryMappingRule(
                urlPath.Split('/', StringSplitOptions.RemoveEmptyEntries),
                localBasePath
            ));
        }

        public override bool CanWorkWith(WebProgressTask task)
        {
            var path = ShortenPath(task.Request.Location.DocumentPathTiles);
            if (path == null)
                return false;
            foreach (var rule in mappingRules)
                if (rule.MapRequest(path.Value.Span, task))
                    return true;
            return false;
        }

        public override Task ProgressTask(WebProgressTask task)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Shortens the input path by removing any "." and ".." sequences. If the input path
        /// is invalid this method returns null.
        /// </summary>
        /// <param name="path">the input path to shorten</param>
        /// <returns>the shortened path or null if invalid input</returns>
        /// <remarks>
        /// Invalid paths are paths that starts with ".." like "../foo/bar".
        /// </remarks>
        /// <example>
        /// // always true
        /// string.Join("/", ShortenPath("foo/bar/../baz".Split('/'))) == "foo/baz";
        /// // always true
        /// ShortenPath("../foo".Split('/')) == null;
        /// </example>
        public static ReadOnlyMemory<string>? ShortenPath(ReadOnlySpan<string> path)
        {
            Memory<string> output = new string[path.Length];
            int offset = 0;
            for (int i = 0; i < path.Length; ++i)
            {
                if (path[i] == ".")
                    continue;
                if (path[i] == "..")
                {
                    if (offset == 0)
                        return null;
                    offset--;
                    continue;
                }
                output.Span[offset] = path[i];
                offset++;
            }
            return output[..offset];
        }
    }
}
