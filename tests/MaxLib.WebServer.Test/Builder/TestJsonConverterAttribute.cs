using System;
using System.Text.Json;
using MaxLib.WebServer.Builder;
using MaxLib.WebServer.Builder.Converter;
using MaxLib.WebServer.Builder.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder
{
    [TestClass]
    public class TestJsonConverterAttribute
    {
        private sealed class Dto
        {
            public string? Name { get; set; }
        }

        // a converter targeting JsonElement naturally returns the element it was handed, unmodified
        public sealed class PassthroughConverter : ICustomJsonConverter
        {
            public object Convert(JsonElement value, Type target) => value;
        }

        [TestMethod]
        public void TestConvertsWellFormedJsonFromAString()
        {
            var attr = new JsonConverterAttribute();
            var converter = ((IConverter)attr).GetConverter(typeof(string), typeof(Dto));

            Assert.IsNotNull(converter);
            var result = converter!("{\"Name\":\"hello\"}");

            Assert.IsInstanceOfType(result, typeof(Dto));
            Assert.AreEqual("hello", ((Dto)result!).Name);
        }

        [TestMethod]
        public void TestMalformedJsonThrowsInsteadOfSilentlyConvertingToNull()
        {
            // a parse error must propagate so Parameter.GetValue can turn it into a 400
            var attr = new JsonConverterAttribute();
            var converter = ((IConverter)attr).GetConverter(typeof(string), typeof(Dto));

            Assert.IsNotNull(converter);
            try
            {
                converter!("{\"Name\":");
                Assert.Fail("expected a JsonException to be thrown");
            }
            catch (JsonException)
            {
                // expected - any subclass (e.g. the internal JsonReaderException) is fine
            }
        }

        [TestMethod]
        public void TestCustomConverterReturningTheElementItselfStaysUsableAfterConversion()
        {
            // the returned element must stay valid after the backing JsonDocument is disposed
            var attr = new JsonConverterAttribute { CustomConverter = typeof(PassthroughConverter) };
            var converter = ((IConverter)attr).GetConverter(typeof(string), typeof(JsonElement));

            Assert.IsNotNull(converter);
            var result = converter!("{\"a\":1}");

            Assert.IsInstanceOfType(result, typeof(JsonElement));
            var element = (JsonElement)result!;
            Assert.AreEqual(1, element.GetProperty("a").GetInt32());
        }
    }
}
