using System;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpStringDataSource : HttpDataSource
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<HttpStringDataSource>();
        static readonly EventId SendEventId = new(0, "Send");

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
            GC.SuppressFinalize(this);
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
                logger.LogInformation(SendEventId, "Connection closed by remote host");
                return m.Position;
            }
            return m.Length;
        }
    }
}
