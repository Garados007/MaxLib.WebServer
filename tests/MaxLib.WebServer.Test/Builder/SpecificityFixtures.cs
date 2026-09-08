using MaxLib.WebServer;
using MaxLib.WebServer.Builder;

#nullable enable

namespace MaxLib.WebServer.Test.Builder
{
    // Two overlapping routes with equal (default) priority: a catch-all {var} segment and a
    // literal segment matching the same URL. Declared in the order least-specific-first, so a
    // fix that just preserved Type.GetMethods() order would happen to "work" here by accident
    // - the companion fixture below declares them in the opposite order to rule that out.
    public class SpecificityVarFirstFixture
    {
        [Path("/spec/{id}")]
        public HttpDataSource ByVar([Var("id")] string id) => new HttpStringDataSource($"var:{id}");

        [Path("/spec/literal")]
        public HttpDataSource ByLiteral() => new HttpStringDataSource("literal");
    }

    public class SpecificityLiteralFirstFixture
    {
        [Path("/spec/literal")]
        public HttpDataSource ByLiteral() => new HttpStringDataSource("literal");

        [Path("/spec/{id}")]
        public HttpDataSource ByVar([Var("id")] string id) => new HttpStringDataSource($"var:{id}");
    }
}
