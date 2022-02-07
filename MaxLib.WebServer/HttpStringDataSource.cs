using MaxLib.IO;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpStringDataSource : HttpDataSource
    {
        private string data = "";
        public string Data
        {
            get => data;
            set => data = value ?? throw new ArgumentNullException(nameof(Data));
        }

        private string encoding;
        public string TextEncoding
        {
            get => encoding;
            set
            {
                encoding = value ?? throw new ArgumentNullException(nameof(value));
                Encoder = Encoding.GetEncoding(value);
            }
        }

        [Obsolete]
        public override bool CanAcceptData => true;

        public override bool CanProvideData => true;

        Encoding Encoder;

        public HttpStringDataSource(string data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Encoder = Encoding.UTF8;
            encoding = Encoder.WebName;
        }

        public override void Dispose()
        {
        }

        public override long? Length()
            => Encoder.GetByteCount(Data);

        protected override async Task<long> WriteStreamInternal(Stream stream, long start, long? stop)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            using (var m = new MemoryStream(Encoder.GetBytes(Data)))
            using (var skip = new SkipableStream(m, start))
            {
                try { return skip.WriteToStream(stream, stop); }
                catch (IOException)
                {
                    WebServerLog.Add(ServerLogType.Information, GetType(), "Send", "Connection closed by remote Host");
                    return m.Position;
                }
            }
        }

        [Obsolete]
        protected override async Task<long> ReadStreamInternal(Stream stream, long? length)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            using (var m = new MemoryStream())
            using (var skip = new SkipableStream(m, 0))
            {
                long total;
                try { total = skip.ReadFromStream(stream, length); }
                catch (IOException)
                {
                    WebServerLog.Add(ServerLogType.Information, GetType(), "Receive", "Connection closed by remote Host");
                    return m.Position;
                }
                Data = Encoder.GetString(m.ToArray());
                return total;
            }
        }
    }
}
