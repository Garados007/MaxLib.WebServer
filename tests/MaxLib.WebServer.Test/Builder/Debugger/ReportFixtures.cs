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
}
