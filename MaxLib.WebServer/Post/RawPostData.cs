using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

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
    public sealed class RawPostData : IPostData
    {
        public string MimeType { get; }

        /// <summary>
        /// The whole request body, as a single entry with no name and no headers of its own.
        /// </summary>
        public MultipartFormData.FormEntry Entry { get; }
            = new MultipartFormData.FormEntry(new Dictionary<string, string>());

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

            // Content-Length is mandatory by the time a ContentStream exists at all
            // (HttpRequestParser rejects Transfer-Encoding and validates Content-Length before
            // ever constructing one), so the body's length is always known here.
            if (MaximumCacheSize >= 0 && content.FullLength > MaximumCacheSize)
            {
                var name = Path.GetTempFileName();
#pragma warning disable CA2000 // already disposed via the using declaration below; the analyzer is confused by the `StorageMapper?.Invoke(task, file) ?? file` fallback
                using var file = new FileStream(name, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
#pragma warning restore CA2000
                using var stream = MultipartFormData.StorageMapper?.Invoke(task, file) ?? file;
                await content.CopyToAsync(stream).ConfigureAwait(false);
                Entry.Set(new FileInfo(name));
            }
            else
            {
                var buffer = new byte[content.UnreadData];
                await content.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
                Entry.Set(buffer);
            }
        }

        public override string ToString()
        {
            if (Entry.Content is ReadOnlyMemory<byte> content)
                return string.Create(CultureInfo.InvariantCulture, $"[{content.Length:#,#0} Bytes]");
            if (Entry.TempFile is FileInfo tempFile && tempFile.Exists)
                return string.Create(CultureInfo.InvariantCulture, $"[{tempFile.Length:#,#0} Bytes in {tempFile.FullName}]");
            return "[empty]";
        }

        public void Dispose()
        {
            Entry.Dispose();
        }
    }
}
