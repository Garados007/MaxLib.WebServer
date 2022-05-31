using System.Text;
using System.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using MaxLib.WebServer.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Post
{
    public class MultipartFormData : IPostData
    {
        public class FormEntry : IDisposable
        {
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
                        WebServerLog.Add(ServerLogType.Information, GetType(), "POST", "Cannot delete temp file");
                    }
                TempFile = null;
            }

            public void Set(FileInfo tempFile)
            {
                Content = null;
                if (TempFile != null && TempFile.FullName != tempFile.FullName)
                    try 
                    {
                        TempFile.Delete();
                    }
                    catch (Exception)
                    {
                        WebServerLog.Add(ServerLogType.Information, GetType(), "POST", "Cannot delete temp file");
                    }
                TempFile = tempFile;
            }

            public virtual void Dispose()
            {
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

        static Regex boundaryRegex = new Regex(
            "boundary\\s*=\\s*(?:\"(?<name>[^\"]*)\"|(?<name>[^\"]*))",
            RegexOptions.Compiled
        );
        static Regex nameRegex = new Regex(
            "[^\\w]name\\s*=\\s*\"(?<name>[^\"]*)\"",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        static Regex filenameRegex = new Regex(
            "[^\\w]filename\\s*=\\s*\"(?<name>[^\"]*)\"",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        static Regex headerSplit = new Regex(
            "^(?<name>[^:\\s]+)\\s*:\\s*(?<value>.*)$",
            RegexOptions.Compiled
        );

        protected virtual FormEntry GetEntry(Dictionary<string, string> header)
        {
            _ = header ?? throw new ArgumentNullException(nameof(header));

            if (header.TryGetValue("Content-Disposition", out string? disposition))
            {
                if (!disposition.StartsWith("form-data"))
                    return new FormEntry(header);
                var nameResult = nameRegex.Match(disposition);
                var name = nameResult.Success ? nameResult.Groups["name"].Value : null;

                var filenameResult = filenameRegex.Match(disposition);
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

        public async Task SetAsync(WebProgressTask task, IO.ContentStream content, string options)
        {
            var match = boundaryRegex.Match(options);
            var boundary = match.Success ? match.Groups["name"].Value : "";
            boundary = $"--{boundary}";
            ReadOnlyMemory<byte> rawBoundary = Encoding.UTF8.GetBytes(boundary);

            Entries.Clear();
            using var reader = new NetworkReader(content, null, true);

            // parse the content
            while (true)
            {
                // expect boundary
                if (reader.ReadLine() != boundary)
                    break;

                // read headers until an empty line is found
                var dict = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                string? line;
                while (!string.IsNullOrWhiteSpace(line = reader.ReadLine()))
                {
                    var header = headerSplit.Match(line);
                    if (!header.Success)
                        break;
                    dict.Add(header.Groups["name"].Value, header.Groups["value"].Value);
                }

                var entry = GetEntry(dict);

                var storeInTemp = (AlwaysStoreFiles && entry is FormDataFile) ||
                    (MaximumCacheSize >= 0 && content.FullLength > MaximumCacheSize);

                // read the content of these entries
                if (storeInTemp)
                {
                    var name = Path.GetTempFileName();
                    using var file = new FileStream(name, FileMode.OpenOrCreate, FileAccess.Write,
                        FileShare.None
                    );
                    using var stream = StorageMapper?.Invoke(task, file) ?? file;
                    await reader.ReadUntilAsync(rawBoundary, stream).ConfigureAwait(false);
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
            content.Discard();
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
        /// will automatically deleted from <see cref="Services.HttpResponseCreator" />.
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
            sb.AppendLine($"[{Entries.Count:#,#0} Entries]");
            var boundary = new string('-', 20);
            foreach (var entry in Entries)
            {
                sb.AppendLine(boundary);
                foreach (var (key, value) in entry.Header)
                    sb.AppendLine($"{key}: {value}");
                sb.AppendLine();
                if (entry.Content != null)
                    sb.AppendLine($"[{entry.Content.Value.Length:#,#0} Bytes]");
                if (entry.TempFile != null && entry.TempFile.Exists)
                    sb.AppendLine($"[{entry.TempFile.Length:#,#0} Bytes in {entry.TempFile.FullName}]");
            }
            sb.AppendLine(boundary);
            return sb.ToString();
        }

        public void Dispose()
        {
            Entries.ForEach(x => x.Dispose());
        }
    }
}