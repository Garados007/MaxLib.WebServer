using System.Text;
using System;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Post
{
    public sealed class UnknownPostData : IPostData
    {
        public IO.ContentStream Data { get; private set; }

        public string MimeType { get; }

        public UnknownPostData(IO.ContentStream data, string? mime)
        {
            MimeType = mime ?? WebServer.MimeType.ApplicationOctetStream;
            Data = data;
        }

        public Task SetAsync(IO.ContentStream content, string options)
        {
            Data = content;
            return Task.CompletedTask;
        }

        public override string ToString()
        {
            return $"[{Data.Length:#,#0} Bytes]";
        }

        public void Dispose()
        {
            Data.Discard();
        }
    }
}