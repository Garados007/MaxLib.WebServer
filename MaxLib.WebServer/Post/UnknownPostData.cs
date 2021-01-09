using System.Text;
using System;

#nullable enable

namespace MaxLib.WebServer.Post
{
    public sealed class UnknownPostData : IPostData
    {
        public ReadOnlyMemory<byte> Data { get; private set; }

        public string MimeType { get; }

        public UnknownPostData(string? mime)
        {
            MimeType = mime ?? WebServer.MimeType.ApplicationOctetStream;
        }

        public UnknownPostData(ReadOnlyMemory<byte> data, string? mime)
            : this(mime)
        {
            Data = data;
        }

        public T Get<T>(string key)
        {
            throw new NotSupportedException();
        }

        [Obsolete]
        public void Set(string content, string options)
        {
            Data = Encoding.UTF8.GetBytes(content);
        }

        public void Set(ReadOnlyMemory<byte> content, string options)
        {
            Data = content;
        }

        public override string ToString()
        {
            return $"[{Data.Length:#,#0} Bytes]";
        }
    }
}