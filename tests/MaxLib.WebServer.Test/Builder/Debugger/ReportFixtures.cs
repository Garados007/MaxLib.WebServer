using System;
using MaxLib.WebServer.Builder;
using MaxLib.WebServer.Builder.Tools;

#nullable enable

namespace MaxLib.WebServer.Test.Builder.Debugger
{
    [Ignore]
    public class IgnoredTypeFixture : Service
    {
        [Path("/ignored")]
        public void M() { }
    }

    public abstract class AbstractTypeFixture : Service
    {
        [Path("/abstract-type")]
        public void M() { }
    }

    public class GenericTypeFixture<T> : Service
    {
        [Path("/generic-type")]
        public void M() { }
    }

    public class NoConstructorTypeFixture : Service
    {
        public NoConstructorTypeFixture(int _) { }

        [Path("/no-ctor")]
        public void M() { }
    }

    public class ThrowingConstructorFixture : Service
    {
        public ThrowingConstructorFixture() => throw new InvalidOperationException("boom");

        [Path("/throwing")]
        public void M() { }
    }

    public class HealthyFixture : Service
    {
        [Path("/healthy")]
        public HttpDataSource M() => new HttpStringDataSource("ok");
    }

    public class NestedTypeFixture : Service
    {
        [Path("/nested-outer")]
        public void Outer() { }

        public class Inner : Service
        {
            [Path("/nested-inner")]
            public HttpDataSource M() => new HttpStringDataSource("inner");
        }
    }

    public abstract class AbstractMethodFixture : Service
    {
        [Path("/abstract-method")]
        public abstract void M();
    }

    public class NoParameterlessCtorConverter : IDataConverter
    {
        public NoParameterlessCtorConverter(int _) { }

        public Func<object, HttpDataSource?>? GetConverter(Type data) => null;
    }

    public class MethodFixtures : Service
    {
        [Ignore]
        public void IgnoredMethod() { }

        internal void NotPublicMethod() { }

        public void GenericMethod<T>() { }

        public void ParamNoConverterMethod([Var] System.Text.StringBuilder sb) { }

        public void WorkingParamMethod([Var] string x) { }

        public void ParamNoCoreConverterMethod(string plain) { }

        public void ParamMissingConverterInstanceMethod(
            [Converter(typeof(IConverter))][Var] string x
        )
        { }

        [return: DataConverter(typeof(string))]
        public string ResultInvalidConverterTypeMethod() => "x";

        [return: DataConverter(typeof(NoParameterlessCtorConverter))]
        public string ResultCannotCreateConverterInstanceMethod() => "x";

        public int ResultNoConverterMethod() => 1;
    }

    public class DupHighFixture : Service
    {
        [Path("/dup")]
        [Priority(WebServicePriority.High)]
        public HttpDataSource M() => new HttpStringDataSource("high");
    }

    public class DupLowFixture : Service
    {
        [Path("/dup")]
        [Priority(WebServicePriority.Low)]
        public HttpDataSource M() => new HttpStringDataSource("low");
    }

    public class GetOnlyFixture : Service
    {
        [Path("/only-get")]
        [Method("GET")]
        public HttpDataSource M() => new HttpStringDataSource("got");
    }

    [Path("/grouped", Prefix = true)]
    public class GroupedFixture : Service
    {
        [Path("/grouped/only")]
        public HttpDataSource M() => new HttpStringDataSource("grouped");
    }

    [Path("/inner-dup", Prefix = true)]
    public class InnerDupFixture : Service
    {
        [Path("/inner-dup/x")]
        [Priority(WebServicePriority.High)]
        public HttpDataSource A() => new HttpStringDataSource("a");

        [Path("/inner-dup/x")]
        [Priority(WebServicePriority.Low)]
        public HttpDataSource B() => new HttpStringDataSource("b");
    }

    [Path("/none-matches", Prefix = true)]
    public class NoMatchingChildFixture : Service
    {
        [Path("/none-matches/only")]
        [Method("POST")]
        public HttpDataSource M() => new HttpStringDataSource("x");
    }

    public class CoreParamFixture : Service
    {
        [Path("/core-param")]
        public HttpDataSource M(System.Net.IPEndPoint endpoint) => new HttpStringDataSource(endpoint.ToString());
    }

    public class ConversionFailureFixture : Service
    {
        [Path("/conv-fail")]
        public HttpDataSource M([Get] int id) => new HttpStringDataSource(id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public sealed class AlwaysFailRuleAttribute : RuleAttributeBase
    {
        public override bool CanWorkWith(WebProgressTask task, System.Collections.Generic.Dictionary<string, object?> vars)
            => false;

        public override string ToString() => "AlwaysFail";
    }

    public class NonExplainableRuleFixture : Service
    {
        [AlwaysFailRule]
        [Path("/non-explainable")]
        public HttpDataSource M() => new HttpStringDataSource("x");
    }

    public class AlwaysRejectService : WebService
    {
        public AlwaysRejectService() : base(ServerStage.CreateDocument) { }

        public override bool CanWorkWith(WebProgressTask task) => false;

        public override System.Threading.Tasks.Task ProgressTask(WebProgressTask task)
            => System.Threading.Tasks.Task.CompletedTask;
    }
}
