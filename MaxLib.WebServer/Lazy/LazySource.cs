using MaxLib.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Lazy
{
    [Serializable]
    public class LazySource : HttpDataSource
    {
        public LazySource(WebProgressTask task, LazyEventHandler handler)
        {
            this.task = new LazyTask(task ?? throw new ArgumentNullException(nameof(task)));
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public LazyEventHandler Handler { get; private set; }

        readonly LazyTask task;
        HttpDataSource[]? list;

        public IEnumerable<HttpDataSource> GetAllSources()
        {
            return list ?? Handler(task);
        }

        public override long? Length()
        {
            if (list == null) 
                list = GetAllSources().ToArray();
            long sum = 0;
            foreach (var entry in list)
            {
                var length = entry.Length();
                if (length == null)
                    return null;
                sum += length.Value;
            }
            return sum;
        }

        public override void Dispose()
        {
            if (list != null)
                foreach (var s in list)
                    s.Dispose();
        }

        protected override async Task<long> WriteStreamInternal(Stream stream)
        {
            long total = 0;
            foreach (var s in GetAllSources())
            {
                total += await s.WriteStream(stream).ConfigureAwait(false);
            }
            return total;
        }
    }
}
