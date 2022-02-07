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

        protected override async Task<long> WriteStreamInternal(Stream stream)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            using var m = new MemoryStream(Encoder.GetBytes(Data));
            try { await m.CopyToAsync(stream).ConfigureAwait(false); }
            catch (IOException)
            {
                WebServerLog.Add(ServerLogType.Information, GetType(), "Send", "Connection closed by remote Host");
                return m.Position;
            }
            return m.Length;
        }
    }
}
