using System;
using System.Text;

namespace MaxLib.WebServer.Builder.Debugger.Example
{
    /// <summary>
    /// Deliberately excluded from the builder via [Ignore] - open the debugger and search for
    /// "IgnoredDemoService" to see it listed as Ignored/TypeIgnoredByAttribute.
    /// </summary>
    [Ignore]
    public class IgnoredDemoService : Service
    {
        [Path("/broken/ignored")]
        public HttpDataSource M() => new HttpStringDataSource("you should never see this");
    }

    /// <summary>
    /// Abstract types can never be instantiated by the generator, so this whole type is skipped.
    /// </summary>
    public abstract class AbstractDemoService : Service
    {
        [Path("/broken/abstract")]
        public abstract HttpDataSource M();
    }

    /// <summary>
    /// A common real-world mistake: adding a constructor parameter (e.g. for dependency
    /// injection) without also providing a parameterless one. The generator can only construct
    /// types via `new()`, so this type is skipped entirely - none of its methods are ever built,
    /// even the ones that look otherwise fine.
    /// </summary>
    public class NoConstructorDemoService : Service
    {
        private readonly string config;

        public NoConstructorDemoService(string config)
        {
            this.config = config;
        }

        [Path("/broken/no-constructor")]
        public HttpDataSource M() => new HttpStringDataSource(config);
    }

    /// <summary>
    /// The constructor throws once the generator tries to create an instance to bind methods to.
    /// The exception is caught and attributed to exactly this method - every other service in
    /// the same scan still builds fine. Open the debugger and search for
    /// "ThrowingConstructorDemoService" to see it reported as Failed/MethodConstructorThrew.
    /// </summary>
    public class ThrowingConstructorDemoService : Service
    {
        public ThrowingConstructorDemoService()
            => throw new InvalidOperationException("simulated misconfiguration in the constructor");

        [Path("/broken/throwing-constructor")]
        public HttpDataSource M() => new HttpStringDataSource("you should never see this");
    }

    /// <summary>
    /// A very common gotcha: the class-level [Path] restricts the URL to that exact path unless
    /// you also set Prefix = true. Without it, no method below can ever be reached, because the
    /// class-level rule already rejects any longer URL before the method rules are even checked.
    /// This is a REAL, live endpoint that 404s - click it, then use the debugger's request
    /// simulator on "/broken/forgot-prefix/detail" to see exactly why.
    /// </summary>
    [Path("/broken/forgot-prefix")]
    public class ForgotPrefixDemoService : Service
    {
        [Path("/broken/forgot-prefix/detail")]
        public HttpDataSource M() => new HttpStringDataSource("you should never see this either");
    }

    /// <summary>
    /// A grab-bag of per-method issues inside an otherwise healthy class.
    /// </summary>
    public class MixedMethodIssuesDemoService : Service
    {
        [Path("/broken/mixed/ok")]
        public HttpDataSource Ok() => new HttpStringDataSource("this one works fine");

        [Ignore]
        [Path("/broken/mixed/ignored-method")]
        public HttpDataSource IgnoredMethod() => new HttpStringDataSource("you should never see this");

        // Not public - the generator only ever binds public methods.
        [Path("/broken/mixed/not-public")]
        internal HttpDataSource NotPublicMethod() => new HttpStringDataSource("you should never see this");

        // [Var] is present, but the built-in converters have no way to turn a string into a
        // StringBuilder -> ParamNoConverterFound.
        [Path("/broken/mixed/bad-param-converter")]
        public HttpDataSource BadParamConverter([Var] StringBuilder value) => new HttpStringDataSource(value.ToString());

        // No attribute at all, and StringBuilder isn't one of the injectable core types either
        // -> ParamNoCoreConverterFound.
        [Path("/broken/mixed/unsupported-param-type")]
        public HttpDataSource UnsupportedParamType(StringBuilder value) => new HttpStringDataSource(value.ToString());

        // Nothing in the default converters knows how to turn a plain int into an HttpDataSource
        // -> ResultNoConverter. Use [return: JsonDataConverter] or return an HttpDataSource
        // yourself instead, like MathService.AddJson() does.
        [Path("/broken/mixed/unsupported-return-type")]
        public int UnsupportedReturnType() => 42;
    }
}
