using System.Text;
using System.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using MaxLib.WebServer.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#nullable enable

namespace MaxLib.WebServer.Post
{
    public partial class MultipartFormData : IPostData
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<MultipartFormData>();
        static readonly EventId PostEventId = new(0, "POST");

        public class FormEntry : IDisposable
        {
            static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<FormEntry>();
            static readonly EventId PostEventId = new(0, "POST");

            public ReadOnlyDictionary<string, string> Header { get; }

            public ReadOnlyMemory<byte>? Content { get; private set; }

            public FileInfo? TempFile { get; private set; }

            public FormEntry(Dictionary<string, string> header)
            {
                _ = header ?? throw new ArgumentNullException(nameof(header));
                Header = new ReadOnlyDictionary<string, string>(header);
            }

            public void Set(ReadOnlyMemory<byte> content)
            {
                Content = content;
                if (TempFile != null && TempFile.Exists)
                    try
                    {
                        TempFile.Delete();
                    }
                    catch (Exception)
                    {
                        logger.LogInformation(PostEventId, "Cannot delete temp file");
                    }
                TempFile = null;
            }

            public void Set(FileInfo tempFile)
            {
                ArgumentNullException.ThrowIfNull(tempFile);
                Content = null;
                if (TempFile != null && TempFile.FullName != tempFile.FullName)
                    try
                    {
                        TempFile.Delete();
                    }
                    catch (Exception)
                    {
                        logger.LogInformation(PostEventId, "Cannot delete temp file");
                    }
                TempFile = tempFile;
            }

            public virtual void Dispose()
            {
                if (TempFile != null && TempFile.Exists)
                    try
                    {
                        TempFile.Delete();
                    }
                    catch (Exception)
                    {
                        logger.LogInformation(PostEventId, "Cannot delete temp file");
                    }
                GC.SuppressFinalize(this);
            }
        }

        public class FormData : FormEntry
        {
            public string Name { get; }

            public FormData(Dictionary<string, string> header, string name)
                : base(header)
            {
                Name = name ?? throw new ArgumentNullException(nameof(name));
            }
        }

        public class FormDataFile : FormData
        {
            public string FileName { get; }

            public string? MimeType => Header.TryGetValue("Content-Type", out string? mime) ? mime : null;

            public FormDataFile(Dictionary<string, string> header, string name, string fileName)
                : base(header, name)
            {
                FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            }
        }

        public string MimeType => WebServer.MimeType.MultipartFormData;

        public List<FormEntry> Entries { get; }
            = new List<FormEntry>();

        protected virtual FormEntry GetEntry(Dictionary<string, string> header)
        {
            _ = header ?? throw new ArgumentNullException(nameof(header));

            if (header.TryGetValue("Content-Disposition", out string? disposition))
            {
                if (!disposition.StartsWith("form-data", StringComparison.Ordinal))
                    return new FormEntry(header);
                var nameResult = nameRegex().Match(disposition);
                var name = nameResult.Success ? nameResult.Groups["name"].Value : null;

                var filenameResult = filenameRegex().Match(disposition);
                var filename = filenameResult.Success ? filenameResult.Groups["name"].Value : null;

                if (filename != null && name != null)
                    return new FormDataFile(header, name, filename);
                if (filename != null)
                    return new FormDataFile(header, filename, filename);
                if (name != null)
                    return new FormData(header, name);
                return new FormEntry(header);
            }
            else return new FormEntry(header);
        }

        /// <summary>
        /// This is the maximum number of bytes the whole POST content can have to have its parts
        /// cached in memory. If the whole POST content is larger than this all parts will be
        /// stored in individual files.
        /// <br />
        /// If this is set to a negative number all content will be stored in memory regardless its
        /// size.
        /// </summary>
        public static long MaximumCacheSize { get; set; } = 50 * 1024 * 1024;

        /// <summary>
        /// If this option is set to true all files from the POST content will be stored as local
        /// temp files. The setting <see cref="MaximumCacheSize" /> will be ignored for this kind.
        /// </summary>
        public static bool AlwaysStoreFiles { get; set; } = true;

        /// <summary>
        /// The maximum number of parts a single multipart body may contain. This is independent
        /// of <see cref="MaximumCacheSize" />/<see cref="Services.HttpRequestParser.MaxContentLength" />:
        /// a body composed of a huge number of minimal parts can stay well under any byte-size
        /// limit while still being expensive to process, since each part carries its own
        /// object/dictionary/regex overhead regardless of how few raw bytes it represents.
        /// Exceeding this rejects the request with <see
        /// cref="HttpStateCode.RequestEntityTooLarge" />. Set this to a negative value to
        /// disable this check. Default is 10,000.
        /// </summary>
        public static int MaximumPartCount { get; set; } = 10_000;

        /// <summary>
        /// The maximum length, in characters, of a part's boundary line or header line. Exceeding it rejects the
        /// request with <see cref="HttpStateCode.RequestHeaderFieldsTooLarge" />. Use a negative value to disable
        /// the check. Default is 8 KB.
        /// </summary>
        public static long MaxPartHeaderLineLength { get; set; } = 8192;

        public async Task SetAsync(WebProgressTask task, IO.ContentStream content, string options)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(content);

            var match = boundaryRegex().Match(options);
            var boundaryName = match.Success ? match.Groups["name"].Value : "";
            if (string.IsNullOrEmpty(boundaryName))
            {
                // a missing/empty boundary can never be parsed correctly: falling back to
                // "--" as the delimiter is a 2-byte sequence virtually guaranteed to occur
                // inside real content, producing nonsensical/truncated entries instead of a
                // clean rejection
                task.Response.StatusCode = HttpStateCode.BadRequest;
                task.NextStage = ServerStage.CreateResponse;
                await content.DiscardAsync().ConfigureAwait(false);
                return;
            }
            var boundary = $"--{boundaryName}";
            ReadOnlyMemory<byte> rawBoundary = Encoding.UTF8.GetBytes("\r\n" + boundary);

            Entries.Clear();
            using var reader = new NetworkReader(content, null, true);

            async Task RejectHeaderTooLarge()
            {
                task.Response.StatusCode = HttpStateCode.RequestHeaderFieldsTooLarge;
                task.NextStage = ServerStage.CreateResponse;
                await content.DiscardAsync().ConfigureAwait(false);
            }

            // parse the content
            var firstPart = true;
            while (true)
            {
                if (!firstPart)
                {
                    // consume the CRLF that precedes this boundary; it was left unread by
                    // the previous ReadUntilAsync call, since it is now part of the search
                    // marking above rather than the previous part's content. The very
                    // first boundary of the body has no preceding CRLF to consume (per RFC
                    // 2046, it may be the first line of the body).
                    var crlf = await reader.ReadBytesAsync(2).ConfigureAwait(false);
                    if (crlf.Length != 2 || crlf[0] != (byte)'\r' || crlf[1] != (byte)'\n')
                        break;
                }
                firstPart = false;

                // expect boundary
                string? boundaryLine;
                try
                {
                    boundaryLine = await reader.ReadLineAsync(MaxPartHeaderLineLength).ConfigureAwait(false);
                }
                catch (IO.ReadLineOverflowException)
                {
                    await RejectHeaderTooLarge().ConfigureAwait(false);
                    return;
                }
                if (boundaryLine != boundary)
                    break;

                if (MaximumPartCount >= 0 && Entries.Count >= MaximumPartCount)
                {
                    // reject before spending any work parsing this (excess) part's headers
                    // or content - a huge part count is itself the attack, regardless of
                    // how small each individual part is
                    task.Response.StatusCode = HttpStateCode.RequestEntityTooLarge;
                    task.NextStage = ServerStage.CreateResponse;
                    await content.DiscardAsync().ConfigureAwait(false);
                    return;
                }

                // read headers until an empty line is found
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string? line;
                try
                {
                    while (!string.IsNullOrWhiteSpace(line = await reader.ReadLineAsync(MaxPartHeaderLineLength).ConfigureAwait(false)))
                    {
                        var header = headerSplit().Match(line);
                        if (!header.Success)
                            break;
                        // last-wins on a repeated header name within one part (Dictionary.Add would throw)
                        dict[header.Groups["name"].Value] = header.Groups["value"].Value;
                    }
                }
                catch (IO.ReadLineOverflowException)
                {
                    await RejectHeaderTooLarge().ConfigureAwait(false);
                    return;
                }

                var entry = GetEntry(dict);

                var storeInTemp = (AlwaysStoreFiles && entry is FormDataFile) ||
                    (MaximumCacheSize >= 0 && content.FullLength > MaximumCacheSize);

                // read the content of these entries
                if (storeInTemp)
                {
                    var name = Path.GetTempFileName();
                    try
                    {
#pragma warning disable CA2000 // already disposed via the using declaration below; the analyzer is confused by the `StorageMapper?.Invoke(task, file) ?? file` fallback
                        using var file = new FileStream(name, FileMode.OpenOrCreate, FileAccess.Write,
                            FileShare.None
                        );
#pragma warning restore CA2000
                        using var stream = StorageMapper?.Invoke(task, file) ?? file;
                        await reader.ReadUntilAsync(rawBoundary, stream).ConfigureAwait(false);
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
                            logger.LogInformation(PostEventId, "Cannot delete temp file");
                        }
                        throw;
                    }
                    entry.Set(new FileInfo(name));
                }
                else
                {
                    entry.Set(await reader.ReadUntilAsync(rawBoundary).ConfigureAwait(false));
                }

                // add new entry
                Entries.Add(entry);
            }

            // there should nothing left but to be sure just discard the rest
            await content.DiscardAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// This maps the file stream that is used to cache entries on the local disk. This is
        /// usefull if you want to add a layer of compression, encryption or throttleling between
        /// the original data received from the client and the local file.
        /// <br/>
        /// The default behavior is to take the data as it is and dump it into the target file
        /// without any processing in between. This can leak confidential data if your file system
        /// is not secure enough.
        /// <br/>
        /// Any temp file that is not moved away until the processing of the request is finished
        /// is automatically deleted by <see cref="FormEntry.Dispose" /> once the request's
        /// <see cref="HttpPost" /> is disposed — which <see cref="Services.HttpResponseCreator" />
        /// does after the response has been fully sent.
        /// <br/>
        /// This stream is only used for storing the data from the POST request. After that this
        /// will automatically disposed. The entries contain only the references to the files as
        /// <see cref="FileInfo" />. If you want to decompress, decrypt or manipulate them you have
        /// to do this in your business logic when you use them.
        /// </summary>
        public static Func<WebProgressTask, Stream, Stream>? StorageMapper { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"[{Entries.Count:#,#0} Entries]");
            var boundary = new string('-', 20);
            foreach (var entry in Entries)
            {
                sb.AppendLine(boundary);
                foreach (var (key, value) in entry.Header)
                    sb.AppendLine(CultureInfo.InvariantCulture, $"{key}: {value}");
                sb.AppendLine();
                if (entry.Content != null)
                    sb.AppendLine(CultureInfo.InvariantCulture, $"[{entry.Content.Value.Length:#,#0} Bytes]");
                if (entry.TempFile != null && entry.TempFile.Exists)
                    sb.AppendLine(CultureInfo.InvariantCulture, $"[{entry.TempFile.Length:#,#0} Bytes in {entry.TempFile.FullName}]");
            }
            sb.AppendLine(boundary);
            return sb.ToString();
        }

        public void Dispose()
        {
            Entries.ForEach(x => x.Dispose());
            GC.SuppressFinalize(this);
        }

        [GeneratedRegex("boundary\\s*=\\s*(?:\"(?<name>[^\"]*)\"|(?<name>[^\";\\s]*))")]
        private static partial Regex boundaryRegex();
        [GeneratedRegex("[^\\w]name\\s*=\\s*\"(?<name>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex nameRegex();
        [GeneratedRegex("[^\\w]filename\\s*=\\s*\"(?<name>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex filenameRegex();
        [GeneratedRegex("^(?<name>[^:\\s]+)\\s*:\\s*(?<value>.*)$")]
        private static partial Regex headerSplit();
    }
}
