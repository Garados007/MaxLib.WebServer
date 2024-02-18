using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Remote
{
    internal class MarshalContainer : MarshalByRefObject
    {
        public HttpDataSource? Origin { get; private set; }

        public void SetOrigin(HttpDataSource origin)
        {
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
        }

        public long? Length()
        {
            _ = Origin ?? throw new InvalidOperationException("Origin is not set");
            return Origin.Length();
        }

        public void Dispose()
        {
            _ = Origin ?? throw new InvalidOperationException("Origin is not set");
            Origin.Dispose();
        }

        public Task<long> WriteStream(Stream stream)
        {
            _ = Origin ?? throw new InvalidOperationException("Origin is not set");
            return Origin.WriteStream(stream);
        }

        public string MimeType()
        {
            _ = Origin ?? throw new InvalidOperationException("Origin is not set");
            return Origin.MimeType;
        }

        public void MimeType(string value)
        {
            _ = Origin ?? throw new InvalidOperationException("Origin is not set");
            Origin.MimeType = value;
        }

        public bool IsLazy()
        {
            _ = Origin ?? throw new InvalidOperationException("Origin is not set");
            return Origin is Lazy.LazySource ||
                (Origin is MarshalSource ms && ms.IsLazy);
        }

        public Collections.MarshalEnumerable<HttpDataSource>? Sources()
        {
            _ = Origin ?? throw new InvalidOperationException("Origin is not set");
            if (Origin is Lazy.LazySource source)
            {
                return new Collections.MarshalEnumerable<HttpDataSource>(
                    source.GetAllSources().Select((s) => new MarshalSource(s)));
            }
            else if (Origin is MarshalSource ms)
            {
                return ms.GetAllSources();
            }
            else
            {
                return null;
            }
        }
    }
}
