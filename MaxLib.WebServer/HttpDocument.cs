using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpDocument : IDisposable
    {
        public List<HttpDataSource> DataSources { get; } = new List<HttpDataSource>();

        private string? primaryMime;

        public string? PrimaryMime
        {
            get { return primaryMime ?? (DataSources.Count == 0 ? null : DataSources[0].MimeType); }
            set => primaryMime = value;
        }

        public string? PrimaryEncoding { get; set; } = null;

        public Dictionary<object?, object?> Information { get; } = new Dictionary<object?, object?>();

        public object? this[object? identifer]
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
