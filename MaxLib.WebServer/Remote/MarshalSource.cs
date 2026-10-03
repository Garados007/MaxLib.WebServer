using System;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Remote
{
    /// <summary>
    /// Marshals an <see cref="HttpDataSource" /> across an <see cref="AppDomain" /> boundary.
    /// </summary>
    /// <remarks>
    /// Cross-<see cref="AppDomain" /> remoting via <see cref="MarshalByRefObject" /> is not
    /// supported on .NET (Core) 5+ and does not function on this library's net8.0/net10.0
    /// targets - <see cref="AppDomain.CreateDomain(string)" /> throws
    /// <see cref="PlatformNotSupportedException" /> at runtime. This type is kept for source
    /// compatibility only and will be removed in a future major version.
    /// </remarks>
    [Serializable]
    [Obsolete("Cross-AppDomain remoting is not supported on .NET (Core) 5+ and this type does " +
        "not function on net8.0/net10.0. It will be removed in a future major version.")]
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
        {
            Container.Dispose();
            GC.SuppressFinalize(this);
        }

        protected override Task<long> WriteStreamInternal(Stream stream)
            => Container.WriteStream(stream);

        public override string MimeType
        {
            get => Container.MimeType();
            set => Container.MimeType(value);
        }

        public Collections.MarshalEnumerable<HttpDataSource>? GetAllSources()
            => Container.Sources();
    }
}
