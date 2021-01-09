using System;
namespace MaxLib.WebServer.Post
{
    public interface IPostData
    {
        string MimeType { get; }

        [Obsolete]
        void Set(string content, string options);

        void Set(ReadOnlyMemory<byte> content, string options);

        T Get<T>(string key);
    }
}