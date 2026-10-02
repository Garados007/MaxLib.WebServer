using System.Text.RegularExpressions;
using System.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#nullable enable

namespace MaxLib.WebServer.Post
{
    public partial class UrlEncodedData : IPostData
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<UrlEncodedData>();
        static readonly EventId SetPostEventId = new(0, "SetPost");

        public string MimeType => WebServer.MimeType.ApplicationXWwwFromUrlencoded;

        public Dictionary<string, string> Parameter { get; }
            = new Dictionary<string, string>();

        /// <summary>
        /// Populated instead of <see cref="Parameter" /> once the whole body exceeds <see
        /// cref="MaximumCacheSize" />. Every entry is represented as a
        /// <see cref="MultipartFormData.FormData" /> instead (its name the url-decoded key),
        /// individually eligible for the same in-memory-vs-temp-file storage decision a
        /// multipart part already gets - a single request can't have any one key or value
        /// larger than its own total declared length, so deciding this once, up front, from
        /// that length is always correct; there is no case where already-collected
        /// <see cref="Parameter" /> entries would need to be converted after the fact.
        /// </summary>
        public MultipartFormData? Overflow { get; private set; }

        /// <summary>
        /// The maximum size, in bytes, the whole body can have to be parsed directly into
        /// <see cref="Parameter" />. Above this, <see cref="Overflow" /> is used instead, so that
        /// a request built from one huge value isn't forced entirely into memory just to
        /// populate a <see cref="Dictionary{TKey, TValue}" />. Set this to a negative value to
        /// always use <see cref="Parameter" /> regardless of size. Default is 50 MB, matching
        /// <see cref="MultipartFormData.MaximumCacheSize" />.
        /// </summary>
        public static long MaximumCacheSize { get; set; } = 50 * 1024 * 1024;

        public void Set(string content, string options)
        {
            _ = content ?? throw new ArgumentNullException(nameof(content));
            Parameter.Clear();
            if (content.Length != 0)
            {
                var tiles = content.Split('&');
                foreach (var tile in tiles)
                {
                    var ind = tile.IndexOf('=', StringComparison.Ordinal);
                    if (ind == -1)
                    {
                        var t = WebServerUtils.DecodeUri(tile);
                        Parameter.TryAdd(t, "");
                    }
                    else
                    {
                        var key = WebServerUtils.DecodeUri(tile[..ind]);
                        var value = ind + 1 == tile.Length ? "" : tile[(ind + 1)..];
                        Parameter.TryAdd(key, WebServerUtils.DecodeUri(value));
                    }
                }
            }
        }

        private static Encoding ResolveEncoding(string options)
        {
            var match = charsetRegex().Match(options);
            Encoding? encoding = null;
            if (match.Success)
                try
                {
                    var charset = match.Groups["charset"].Value;
                    // RFC 9110 §5.6.6: a parameter value may be a quoted-string (charset="iso-8859-1"); the regex
                    // captures the quotes, so unwrap them before the lookup.
                    if (charset.Length >= 2 && charset[0] == '"' && charset[^1] == '"')
                        charset = charset[1..^1];
                    encoding = Encoding.GetEncoding(charset);
                }
                catch (Exception e)
                {
                    logger.LogError(SetPostEventId, e, "Invalid encoding {Charset}", match.Groups["charset"].Value);
                }
            return encoding ?? Encoding.UTF8;
        }

        /// <remarks>
        /// SetAsync must be called at most once per instance; a second call leaks the previous call's
        /// temp files.
        /// </remarks>
        public async Task SetAsync(WebProgressTask task, IO.ContentStream content, string options)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(content);
            var encoding = ResolveEncoding(options);

            if (MaximumCacheSize < 0 || content.FullLength <= MaximumCacheSize)
            {
                var buffer = new byte[content.UnreadData];
                await content.ReadExactlyAsync(buffer.AsMemory()).ConfigureAwait(false);
                Set(encoding.GetString(buffer), options);
                return;
            }

            // the body is large enough that a single key or value could plausibly need
            // file-backed storage; spill it to disk first - streamed, never buffered as one
            // block - and parse token-by-token from there instead
            Parameter.Clear();
            Overflow = new MultipartFormData();
            var rawFileName = Path.GetTempFileName();
            using var rawFile = new FileStream(
                rawFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                4096, FileOptions.DeleteOnClose
            );
            await content.CopyToAsync(rawFile).ConfigureAwait(false);
            rawFile.Position = 0;
            await ParseTokensAsync(task, rawFile, encoding).ConfigureAwait(false);
        }

        private async Task ParseTokensAsync(WebProgressTask task, Stream rawFile, Encoding encoding)
        {
            var seenKeys = new HashSet<string>();
            using var tokenBuffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await rawFile.ReadAsync(chunk.AsMemory()).ConfigureAwait(false)) > 0)
            {
                var start = 0;
                for (var i = 0; i < read; i++)
                {
                    // a literal '&' can only ever appear as a delimiter here - any '&' that was
                    // part of a key/value's actual content was necessarily percent-encoded
                    // (%26) by any conformant client, so splitting on the raw byte is safe
                    // regardless of the request's declared charset
                    if (chunk[i] != (byte)'&')
                        continue;
                    tokenBuffer.Write(chunk.AsSpan(start, i - start));
                    await AddTokenAsync(task, tokenBuffer, encoding, seenKeys).ConfigureAwait(false);
                    tokenBuffer.SetLength(0);
                    start = i + 1;
                }
                if (start < read)
                    tokenBuffer.Write(chunk.AsSpan(start, read - start));
            }
            if (tokenBuffer.Length > 0)
                await AddTokenAsync(task, tokenBuffer, encoding, seenKeys).ConfigureAwait(false);
        }

        private async Task AddTokenAsync(
            WebProgressTask task, MemoryStream tokenBuffer, Encoding encoding, HashSet<string> seenKeys
        )
        {
            // a token here is still the raw, percent-encoded "key" or "key=value" text; decode
            // it exactly the same way Set(string, string) already does for the small-body path
            var text = encoding.GetString(tokenBuffer.ToArray());
            var ind = text.IndexOf('=', StringComparison.Ordinal);
            string key, value;
            if (ind == -1)
            {
                key = WebServerUtils.DecodeUri(text);
                value = "";
            }
            else
            {
                key = WebServerUtils.DecodeUri(text[..ind]);
                value = WebServerUtils.DecodeUri(text[(ind + 1)..]);
            }

            // first-value-wins, matching Parameter.TryAdd's semantics in the small-body path
            if (!seenKeys.Add(key))
                return;

            var entry = new MultipartFormData.FormData(new Dictionary<string, string>(), key);
            var valueBytes = Encoding.UTF8.GetBytes(value);
            if (MaximumCacheSize >= 0 && valueBytes.LongLength > MaximumCacheSize)
            {
                var name = Path.GetTempFileName();
                try
                {
#pragma warning disable CA2000 // already disposed via the using declaration below; the analyzer is confused by the `StorageMapper?.Invoke(task, file) ?? file` fallback
                    using var file = new FileStream(name, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
#pragma warning restore CA2000
                    using var stream = MultipartFormData.StorageMapper?.Invoke(task, file) ?? file;
                    await stream.WriteAsync(valueBytes).ConfigureAwait(false);
                }
                catch
                {
                    // the write above never finished, so `name` is not attached to `entry` and FormEntry.Dispose
                    // will never delete it
                    try
                    {
                        File.Delete(name);
                    }
                    catch (Exception)
                    {
                        logger.LogInformation(SetPostEventId, "Cannot delete temp file");
                    }
                    throw;
                }
                entry.Set(new FileInfo(name));
            }
            else
            {
                entry.Set(valueBytes);
            }
            Overflow!.Entries.Add(entry);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var (key, value) in Parameter)
                sb.AppendLine(CultureInfo.InvariantCulture, $"{key}: {value}");
            return sb.ToString();
        }

        /// <remarks>
        /// Not thread-safe: await SetAsync before calling Dispose, otherwise temp files may leak.
        /// </remarks>
        public void Dispose()
        {
            Overflow?.Dispose();
            GC.SuppressFinalize(this);
        }

        [GeneratedRegex("charset\\s*=\\s*(?<charset>[^\\s;]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex charsetRegex();
    }
}
