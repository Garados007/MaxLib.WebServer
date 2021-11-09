using System;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Post
{
    public interface IPostData : IDisposable
    {
        string MimeType { get; }

        Task SetAsync(IO.ContentStream content, string options);
    }
}