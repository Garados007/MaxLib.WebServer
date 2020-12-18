using System;
using System.Collections.Generic;
using System.Linq;

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpDocument : IDisposable
    {
        public List<HttpDataSource> DataSources { get; } = new List<HttpDataSource>();

        public string PrimaryMime
        {
            get { return DataSources.Count == 0 ? null : DataSources[0].MimeType; }
        }

        public string PrimaryEncoding { get; set; } = null;

        [Obsolete("Use WebProgressTask.Request. this will be removed in a future release.")]
        public HttpRequestHeader RequestHeader { get; set; } = new HttpRequestHeader();

        [Obsolete("Use WebProgressTask.Response. this will be removed in a future release.")]
        public HttpResponseHeader ResponseHeader { get; set; } = new HttpResponseHeader();
        
        public Dictionary<object, object> Information { get; } = new Dictionary<object, object>();

        public object this[object identifer]
        {
            get => Information[identifer];
            set => Information[identifer] = value;
        }

        public void Dispose()
        {
            foreach (var ds in DataSources.ToArray()) ds.Dispose();
            DataSources.Clear();
            foreach (var kvp in Information.ToArray())
            {
                if (kvp.Key is IDisposable) ((IDisposable)kvp.Key).Dispose();
                if (kvp.Value is IDisposable) ((IDisposable)kvp.Value).Dispose();
            }
            Information.Clear();
        }
    }
}
