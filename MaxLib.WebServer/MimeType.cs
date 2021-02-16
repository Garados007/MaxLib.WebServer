using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    /// <summary>
    /// This class holds some constants for popular mime types. It also holds some
    /// functionality for working with mime types.
    /// </summary>
    public static class MimeType
    {

        public const string ApplicationXWwwFromUrlencoded = "application/x-www-form-urlencoded";
        /// <summary>
        /// Microsoft Excel files (*.xls *.xla)
        /// </summary>
        public const string ApplicationMsexcel = "application/msexcel";
        /// <summary>
        /// Microsoft Powerpoint files (*.ppt *.ppz *.pps *.pot)
        /// </summary>
        public const string ApplicationMspowerpoint = "application/mspowerpoint";
        /// <summary>
        /// Microsoft Word files (*.doc *.dot)
        /// </summary>
        public const string ApplicationMsword = "application/msword";
        /// <summary>
        /// GNU Zip-files (*.gz)
        /// </summary>
        public const string ApplicationGzip = "application/gzip";
        /// <summary>
        /// JSON files (*.json)
        /// </summary>
        public const string ApplicationJson = "application/json";
        /// <summary>
        /// not specified files, .e.g executable files (*.bin *.exe *.com *.dll *.class)
        /// </summary>
        public const string ApplicationOctetStream = "application/octet-stream";
        /// <summary>
        /// PDF-files (*.pdf)
        /// </summary>
        public const string ApplicationPdf = "application/pdf";
        /// <summary>
        /// RTF-files (*.rtf)
        /// </summary>
        public const string ApplicationRtf = "application/rtf";
        /// <summary>
        /// XHTML-files (*.htm *.html *.shtml *.xhtml)
        /// </summary>
        public const string ApplicationXhtml = "application/xhtml+xml";
        /// <summary>
        /// XML-files (*.xml)
        /// </summary>
        public const string ApplicationXml = "application/xml";
        /// <summary>
        /// PHP-files (*.php *.phtml)
        /// </summary>
        public const string ApplicationPhp = "application/x-httpd-php";
        /// <summary>
        /// server side JavaScript-files (*.js)
        /// </summary>
        public const string ApplicationJs = "application/x-javascript";
        /// <summary>
        /// ZIP-Archive files (*.zip)
        /// </summary>
        public const string ApplicationZip = "application/zip";
        /// <summary>
        /// MPEG-Audio files (*.mp2)
        /// </summary>
        public const string AudioMpeg = "audio/x-mpeg";
        /// <summary>
        /// WAV-files (*.wav)
        /// </summary>
        public const string AudioWav = "audio/x-wav";
        /// <summary>
        /// GIF-files (*.gif)
        /// </summary>
        public const string ImageGif = "image/gif";
        /// <summary>
        /// JPEG-files (*.jpeg *.jpg *.jpe)
        /// </summary>
        public const string ImageJpeg = "image/jpeg";
        /// <summary>
        /// PNG-files (*.png *.pneg)
        /// </summary>
        public const string ImagePng = "image/png";
        /// <summary>
        /// Icon-files (z.B. Favorites-Icons) (*.ico)
        /// </summary>
        public const string ImageIcon = "image/x-icon";
        /// <summary>
        /// multipart data; each part in an alternative with equal value to others
        /// </summary>
        public const string MultipartAlternative = "multipart/alternative";
        /// <summary>
        /// multipart data mit byte specifications
        /// </summary>
        public const string MultipartByteranges = "multipart/byteranges";
        /// <summary>
        /// encrypted multipart data
        /// </summary>
        public const string MultipartEncrypted = "multipart/encrypted";
        /// <summary>
        /// multipart data from a HTTP formular (z.B. File-Upload) 
        /// </summary>
        public const string MultipartFormData = "multipart/form-data";
        /// <summary>
        /// multipart data without a connection of the parts
        /// </summary>
        public const string MultipartMixed = "multipart/mixed";
        /// <summary>
        /// CSS Stylesheet-files (*.css)
        /// </summary>
        public const string TextCss = "text/css";
        /// <summary>
        /// HTML-files (*.htm *.html *.shtml)
        /// </summary>
        public const string TextHtml = "text/html";
        /// <summary>
        /// JavaScript-files (*.js)
        /// </summary>
        public const string TextJs = "text/javascript";
        /// <summary>
        /// plain text files (*.txt)
        /// </summary>
        public const string TextPlain = "text/plain";
        /// <summary>
        /// RTF-files (*.rtf)
        /// </summary>
        public const string TextRtf = "text/rtf";
        /// <summary>
        /// XML-files (*.xml)
        /// </summary>
        public const string TextXml = "text/xml";
        /// <summary>
        /// MPEG-Video files (*.mpeg *.mpg *.mpe)
        /// </summary>
        public const string VideoMpeg = "video/mpeg";
        /// <summary>
        /// Microsoft AVI-files (*.avi)
        /// </summary>
        public const string VideoAvi = "video/x-msvideo";
        /// <summary>
        /// Checks if the mime type matches the pattern.
        /// </summary>
        /// <param name="mime">the mime type</param>
        /// <param name="pattern">
        /// the pattern in the same format like the mime type. It can contains * as
        /// placeholder (e.g. "text/plain" matches "text/plain", "text/*", "*/plain" and "*/*")
        /// </param>
        /// <returns>true if mime matches pattern</returns>
        public static bool Check(string mime, string pattern)
        {
            _ = mime ?? throw new ArgumentNullException(nameof(mime));
            _ = pattern ?? throw new ArgumentNullException(nameof(pattern));
            var ind = mime.IndexOf('/');
            if (ind == -1) 
                throw new ArgumentException("no Mime", nameof(mime));
            var ml = mime.Remove(ind).ToLower();
            var mh = mime.Substring(ind + 1).ToLower();
            ind = pattern.IndexOf('/');
            if (ind == -1) 
                throw new ArgumentException("no Mime", nameof(pattern));
            var pl = pattern.Remove(ind).ToLower();
            var ph = pattern.Substring(ind + 1).ToLower();
            return (pl == "*" || pl == ml) && (ph == "*" || ph == mh);
        }

        private static Dictionary<string, string> mimeTypes = new Dictionary<string, string>();

        /// <summary>
        /// Searches for a mime type for a given file extension. This requires that the mime
        /// cache is loaded with <see cref="LoadMimeTypesForExtensions(bool)"/>.
        /// </summary>
        /// <param name="extension">the file extension without a leading dot</param>
        /// <returns>the found mime type</returns>
        public static string? GetMimeTypeForExtension(string extension)
        {
            _ = extension ?? throw new ArgumentNullException(nameof(extension));
            if (mimeTypes.TryGetValue(extension.ToLower(), out string mime))
                return mime;
            else return null;
        }

        /// <summary>
        /// load the data for <see cref="GetMimeTypeForExtension(string)"/>. If no
        /// cache file exists or <paramref name="useLocalCache"/> is false it loads
        /// the data from <a href="http://svn.apache.org/repos/asf/httpd/httpd/trunk/docs/conf/mime.types">
        /// http://svn.apache.org/repos/asf/httpd/httpd/trunk/docs/conf/mime.types
        /// </a>
        /// <br/>
        /// If <paramref name="useLocalCache"/> is true it uses the cache file at 
        /// <c>./mime-cache.json</c>.
        /// </summary>
        /// <param name="useLocalCache">true if to use the cache file</param>
        /// <returns>the task that loads the data</returns>
        public static async Task LoadMimeTypesForExtensions(bool useLocalCache)
        {
            var mimeTypes = new Dictionary<string, string>();
            if (useLocalCache && File.Exists("mime-cache.json"))
            {
                WebServerLog.Add(ServerLogType.Debug, typeof(MimeType), "load mime", "load mime cache");
                using var file = new FileStream("mime-cache.json", FileMode.Open,
                    FileAccess.Read, FileShare.Read
                );
                var doc = await JsonDocument.ParseAsync(file);
                foreach (var entry in doc.RootElement.EnumerateObject())
                {
                    mimeTypes[entry.Name] =  entry.Value.GetString()!;
                }

                WebServerLog.Add(ServerLogType.Debug, typeof(MimeType), "load mime", "mime cache loaded");
            }
            else
            {
                WebServerLog.Add(ServerLogType.Debug, typeof(MimeType), "load mime", "Update Mime Cachce");
                using var wc = new WebClient();
                var reader = new StringReader(await wc.DownloadStringTaskAsync(
                    @"http://svn.apache.org/repos/asf/httpd/httpd/trunk/docs/conf/mime.types"
                ));
                var regex = new Regex(
                    @"^(?<mime>[^#][^\s]*)(\s+(?<extension>\w+))+$",
                    RegexOptions.Compiled
                );
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var match = regex.Match(line);
                    if (!match.Success)
                        continue;
                    foreach (Capture exCap in match.Groups["extension"].Captures)
                    {
                        mimeTypes[exCap.Value] = match.Groups["mime"].Value;
                    }
                }
                if (useLocalCache)
                {
                    using var file = new FileStream("mime-cache.json", FileMode.OpenOrCreate,
                        FileAccess.Write, FileShare.Read);
                    using var writer = new Utf8JsonWriter(file);
                    writer.WriteStartObject();
                    foreach (var (ext, mime) in mimeTypes)
                        writer.WriteString(ext, mime);
                    writer.WriteEndObject();
                    await writer.FlushAsync();
                }
                WebServerLog.Add(ServerLogType.Debug, typeof(MimeType), "load mime", "Mime Cache updated");
            }
            MimeType.mimeTypes = mimeTypes;
        }
    }
}
