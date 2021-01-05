using System.Text;
using System.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

#nullable enable

namespace MaxLib.WebServer.Post
{
    public class MultipartFormData : IPostData
    {
        public class FormEntry
        {
            public ReadOnlyDictionary<string, string> Header { get; }

            public string Content { get; }

            public FormEntry(Dictionary<string, string> header, string content)
            {
                _ = header ?? throw new ArgumentNullException(nameof(header));
                Content = content ?? throw new ArgumentNullException(nameof(content));
                Header = new ReadOnlyDictionary<string, string>(header);
            }
        }

        public class FormData : FormEntry
        {
            public string Name { get; }

            public FormData(Dictionary<string, string> header, string name, string value)
                : base(header, value)
            {
                Name = name ?? throw new ArgumentNullException(nameof(name));
            }
        }

        public class FormDataFile : FormData
        {
            public string FileName { get; }

            public string? MimeType => Header.TryGetValue("Content-Type", out string mime) ? mime : null;

            public FormDataFile(Dictionary<string, string> header, string name, string fileName, string value)
                : base(header, name, value)
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

        public void Set(string content, string options)
        {
            var match = boundaryRegex.Match(options);
            var boundary = match.Success ? match.Groups["name"].Value : "";

            Entries.Clear();
            int index = 0;

            // I dont know but every used boundary is "--" longer at the beginning than it was
            // told in the header. :shrug:
            while (index < content.Length && !IsAtPos(boundary, content, index))
                index++;
            boundary = content.Substring(0, index) + boundary;
            index = 0;

            //normal parsing
            while (index < content.Length)
            {
                // expect boundary
                if (!IsAtPos(boundary, content, index))
                    break;
                index += boundary.Length;
                // expect new line or something
                if (!IsNewline(content, ref index))
                    break;
                // read headers until an empty line is found
                var dict = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                string line;
                while ((line = ReadLine(content, ref index)) != "")
                {
                    var header = headerSplit.Match(line);
                    if (!header.Success)
                        break;
                    dict.Add(header.Groups["name"].Value, header.Groups["value"].Value);
                }
                // read content until boundary found
                var result = new StringBuilder();
                while (index < content.Length && !IsAtPos(boundary, content, index))
                    result.Append(content[index++]);
                // add new entry
                Entries.Add(GetEntry(dict, result.ToString()));
            }
        }

        private string ReadLine(string target, ref int position)
        {
            var result = new StringBuilder();
            while (position < target.Length && !IsNewline(target, ref position))
            {
                result.Append(target[position]);
                position++;
            }
            return result.ToString();
        }

        private bool IsNewline(string target, ref int position)
        {
            switch (target[position])
            {
                case '\r':
                    if (target.Length > position + 1 && target[position + 1] == '\n')
                        position++;
                    goto case '\n';
                case '\n':
                    position++;
                    return true;
                default: return false;
            }
        }

        private bool IsAtPos(string search, string target, int position)
        {
            if (position + search.Length > target.Length)
                return false;
            // search from back to front is slightly better because most engines build their
            // boundary so that the random part is at the end. Therefore we can find 
            // miss matches faster.
            for (int i = search.Length - 1; i >= 0; --i)
                if (search[i] != target[i + position])
                    return false;
            return true;
        }

        protected virtual FormEntry GetEntry(Dictionary<string, string> header, string value)
        {
            _ = header ?? throw new ArgumentNullException(nameof(header));
            _ = value ?? throw new ArgumentNullException(nameof(value));

            if (header.TryGetValue("Content-Disposition", out string disposition))
            {
                if (!disposition.StartsWith("form-data"))
                    return new FormEntry(header, value);
                var nameResult = nameRegex.Match(disposition);
                var name = nameResult.Success ? nameResult.Groups["name"].Value : null;

                var filenameResult = filenameRegex.Match(disposition);
                var filename = filenameResult.Success ? filenameResult.Groups["name"].Value : null;

                if (filename != null && name != null)
                    return new FormDataFile(header, name, filename, value);
                if (filename != null)
                    return new FormDataFile(header, filename, filename, value);
                if (name != null)
                    return new FormData(header, name, value);
                return new FormEntry(header, value);
            }
            else return new FormEntry(header, value);
        }

        public T Get<T>(string key)
        {
            if (!typeof(T).IsAssignableFrom(typeof(string)))
                throw new NotSupportedException();
            var entry = Entries.Find(x => x is FormData d && d.Name == key);
            if (entry != null)
                return (T)(object)entry.Content;
            entry = Entries.Find(x => x is FormDataFile d && d.FileName == key);
            if (entry != null)
                return (T)(object)entry.Content;
            throw new KeyNotFoundException();
        }
    }
}