using System.IO;
using System.Text;
using System.Threading.Tasks;
using MaxLib.WebServer.Builder;
using MaxLib.WebServer.Builder.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder
{
    [TestClass]
    public class TestJsonDataConverterAttribute
    {
        private static async Task<string> Serialize(object? value)
        {
            var attr = new JsonDataConverterAttribute();
            var converter = ((IDataConverter)attr).GetConverter(typeof(object));
            Assert.IsNotNull(converter);
            var source = converter!(value!);
            Assert.IsNotNull(source);
            using var stream = new MemoryStream();
            await source!.WriteStream(stream).ConfigureAwait(false);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        [TestMethod]
        public async Task TestGetConverterSerializesNullAsJsonNull()
        {
            // the null token must not be followed by a second root-level value, which makes the writer throw
            var json = await Serialize(null).ConfigureAwait(false);
            Assert.AreEqual("null", json);
        }

        [TestMethod]
        public async Task TestGetConverterSerializesAnOrdinaryValue()
        {
            var json = await Serialize(new { hello = "world" }).ConfigureAwait(false);
            StringAssert.Contains(json, "\"hello\"");
            StringAssert.Contains(json, "\"world\"");
        }
    }
}
