using MaxLib.IO;
using System;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpFileDataSource : HttpDataSource
    {
        public FileStream? File { get; private set; }

        private string? path = null;
        public virtual string? Path
        {
            get => path;
            set
            {
                if (path == value) return;
                if (File != null) File.Dispose();
                if (value == null) File = null;
                else
                {
                    var fi = new FileInfo(value);
                    if (!fi.Directory.Exists) fi.Directory.Create();
                    File = new FileStream(value, FileMode.OpenOrCreate, FileAccess.Read,
                        FileShare.ReadWrite);
                }
                path = value;
            }
        }

        public HttpFileDataSource(string? path)
        {
            Path = path;
        }

        public override void Dispose()
        {
            Path = null;
        }

        public override long? Length()
            => File?.Length;

        protected override async Task<long> WriteStreamInternal(Stream stream, long start, long? stop)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (File == null)
                return 0;
            File.Position = start;
            using (var skip = new SkipableStream(File, 0))
            {
                try
                {
                    return skip.WriteToStream(stream,
                        stop == null ? null : (long?)(stop.Value - start));
                }
                catch (IOException)
                {
                    WebServerLog.Add(ServerLogType.Information, GetType(), "Send", "Connection closed by remote Host");
                    return File.Position - start;
                }
            }
        }
    }
}
