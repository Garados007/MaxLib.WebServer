using System;
using System.IO;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpFileDataSource : HttpStreamDataSource
    {
        public FileStream File => (FileStream)Stream;

        public virtual string Path { get; }

        public HttpFileDataSource(string path)
            : base(GetStream(path))
        {
            Path = path;
        }

        private static FileStream GetStream(string path)
        {
            _ = path ?? throw new ArgumentNullException(nameof(path));
            if (!System.IO.File.Exists(path))
                throw new FileNotFoundException("required file not found", path);
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
    }
}
