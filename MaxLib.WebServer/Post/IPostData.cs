namespace MaxLib.WebServer.Post
{
    public interface IPostData
    {
        string MimeType { get; }

        void Set(string content, string options);

        T Get<T>(string key);
    }
}