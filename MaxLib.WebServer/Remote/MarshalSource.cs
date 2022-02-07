﻿using System;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Remote
{
    [Serializable]
    public class MarshalSource : HttpDataSource
    {
        public bool IsLazy => Container.IsLazy();

        internal MarshalContainer Container { get; }

        public MarshalSource(HttpDataSource source)
        {
            Container = new MarshalContainer();
            Container.SetOrigin(source ?? throw new ArgumentNullException(nameof(source)));
        }

        public override long? Length()
            => Container.Length();

        public override void Dispose()
            => Container.Dispose();

        protected override Task<long> WriteStreamInternal(Stream stream, long start, long? stop)
            => Container.WriteStream(stream, start, stop);

        [Obsolete]
        public override long? RangeEnd
        {
            get => Container.RangeEnd();
            set => Container.RangeEnd(value);
        }

        [Obsolete]
        public override long RangeStart
        {
            get => Container.RangeStart();
            set => Container.RangeStart(value);
        }

        [Obsolete]
        public override bool TransferCompleteData
        {
            get => Container.TransferCompleteData();
            set => Container.TransferCompleteData(value);
        }

        public override string MimeType
        {
            get => Container.MimeType();
            set => Container.MimeType(value);
        }

        public Collections.MarshalEnumerable<HttpDataSource>? GetAllSources()
            => Container.Sources();
    }
}
