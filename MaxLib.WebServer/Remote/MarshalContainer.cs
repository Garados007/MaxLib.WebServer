using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Remote
{
    internal class MarshalContainer : MarshalByRefObject
    {
        public HttpDataSource Origin { get; private set; }

        public void SetOrigin(HttpDataSource origin)
        {
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
        }

        [Obsolete]
        public bool CanAcceptData()
            => Origin?.CanAcceptData ?? false;

        public bool CanProvideData()
            => Origin?.CanProvideData ?? false;

        public long? Length()
        {
            return Origin.Length();
        }

        public void Dispose()
        {
            Origin.Dispose();
        }

        public Task<long> WriteStream(Stream stream, long start, long? stop)
            => Origin.WriteStream(stream, start, stop);

        [Obsolete]
        public Task<long> ReadStream(Stream stream, long? length)
            => Origin.ReadStream(stream, length);

        [Obsolete]
        public long? RangeEnd()
        {
            return Origin.RangeEnd;
        }

        [Obsolete]
        public void RangeEnd(long? value)
        {
            Origin.RangeEnd = value;
        }

        [Obsolete]
        public long RangeStart()
        {
            return Origin.RangeStart;
        }

        [Obsolete]
        public void RangeStart(long value)
        {
            Origin.RangeStart = value;
        }

        [Obsolete]
        public bool TransferCompleteData()
        {
            return Origin.TransferCompleteData;
        }

        [Obsolete]
        public void TransferCompleteData(bool value)
        {
            Origin.TransferCompleteData = value;
        }

        public string MimeType()
        {
            return Origin.MimeType;
        }

        public void MimeType(string value)
        {
            Origin.MimeType = value;
        }

        public bool IsLazy()
        {
            return Origin is Lazy.LazySource ||
                (Origin is MarshalSource ms && ms.IsLazy);
        }

        public Collections.MarshalEnumerable<HttpDataSource> Sources()
        {
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
