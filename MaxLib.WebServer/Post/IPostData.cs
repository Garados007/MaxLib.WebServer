using System;
namespace MaxLib.WebServer.Post
{
    public interface IPostData
    {
        string MimeType { get; }

        void Set(ReadOnlyMemory<byte> content, string options);

        T Get<T>(string key);
    }
}