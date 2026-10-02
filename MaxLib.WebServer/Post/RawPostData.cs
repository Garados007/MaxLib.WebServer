using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#nullable enable

namespace MaxLib.WebServer.Post
{
    /// <summary>
    /// Handles any request body whose <c>Content-Type</c> has no dedicated <see cref="IPostData" />
    /// (<c>application/json</c>, <c>application/octet-stream</c>, anything else unrecognized, or a
    /// missing <c>Content-Type</c> entirely) by storing the whole body as one nameless
    /// <see cref="MultipartFormData.FormEntry" />, in memory or spilled to a temp file depending on
    /// its size - exactly like an individual multipart part already is.
    /// </summary>
    public sealed partial class RawPostData : IPostData
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<RawPostData>();
        static readonly EventId PostEventId = new(0, "POST");

        public string MimeType { get; }

        /// <summary>
        /// The whole request body, as a single entry with no name and no headers of its own.
        /// </summary>
        public MultipartFormData.FormEntry Entry { get; }
            = new MultipartFormData.FormEntry(new Dictionary<string, string>());

        /// <summary>
        /// The charset this body was declared with, resolved once in <see cref="SetAsync" />.
        /// Falls back to UTF-8 if none or an unknown one was declared.
        /// </summary>
        public Encoding Encoding { get; private set; } = Encoding.UTF8;

        /// <summary>
        /// The maximum number of bytes the body can have to be cached in memory. If the body is
        /// larger than this it is stored in a temp file instead - the same threshold multipart
        /// parts already use. Set this to a negative value to always cache in memory regardless
        /// of size.
        /// </summary>
        public static long MaximumCacheSize { get; set; } = 50 * 1024 * 1024;

        public RawPostData(string? mime)
        {
            MimeType = mime ?? WebServer.MimeType.ApplicationOctetStream;
        }

        public async Task SetAsync(WebProgressTask task, IO.ContentStream content, string options)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(content);
            Encoding = ResolveEncoding(options);

            // Content-Length is mandatory by the time a ContentStream exists at all
            // (HttpRequestParser rejects Transfer-Encoding and validates Content-Length before
            // ever constructing one), so the body's length is always known here.
            if (MaximumCacheSize >= 0 && content.FullLength > MaximumCacheSize)
            {
                var name = Path.GetTempFileName();
                try
                {
#pragma warning disable CA2000 // already disposed via the using declaration below; the analyzer is confused by the `StorageMapper?.Invoke(task, file) ?? file` fallback
                    using var file = new FileStream(name, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
#pragma warning restore CA2000
                    using var stream = MultipartFormData.StorageMapper?.Invoke(task, file) ?? file;
                    await content.CopyToAsync(stream).ConfigureAwait(false);
                }
                catch
                {
                    // the copy above never finished, so `name` is not attached to `Entry` and FormEntry.Dispose
                    // will never delete it
                    try
                    {
                        File.Delete(name);
                    }
                    catch (Exception)
                    {
                        logger.LogInformation(PostEventId, "Cannot delete temp file");
                    }
                    throw;
                }
                Entry.Set(new FileInfo(name));
            }
            else
            {
                var buffer = new byte[content.UnreadData];
                await content.ReadExactlyAsync(buffer.AsMemory()).ConfigureAwait(false);
                Entry.Set(buffer);
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
                    logger.LogError(PostEventId, e, "Invalid encoding {Charset}", match.Groups["charset"].Value);
                }
            return encoding ?? Encoding.UTF8;
        }

        [GeneratedRegex("charset\\s*=\\s*(?<charset>[^\\s;]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex charsetRegex();

        public override string ToString()
        {
            if (Entry.Content is ReadOnlyMemory<byte> content)
                return string.Create(CultureInfo.InvariantCulture, $"[{content.Length:#,#0} Bytes]");
            if (Entry.TempFile is FileInfo tempFile && tempFile.Exists)
                return string.Create(CultureInfo.InvariantCulture, $"[{tempFile.Length:#,#0} Bytes in {tempFile.FullName}]");
            return "[empty]";
        }

        /// <remarks>
        /// Not thread-safe: await SetAsync before calling Dispose, otherwise temp files may leak.
        /// </remarks>
        public void Dispose()
        {
            Entry.Dispose();
        }
    }
}
