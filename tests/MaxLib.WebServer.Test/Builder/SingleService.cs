using MaxLib.WebServer;
using MaxLib.WebServer.Builder;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder
{
    [Path("/foo")]
    public class SingleService
    {
        [WebServer.Builder.Priority(WebServicePriority.High)]
        [Path("/foo/{id}")]
        [return: DataConverter(typeof(MaxLib.WebServer.Builder.Converter.DataConverter))]
        public async Task<HttpDataSource> Foo(HttpRequestHeader request, [Var("id")] int myId, [Get] string bar)
        {
            Assert.AreEqual("/foo/2", request.Location.DocumentPath);
            Assert.AreEqual(2, myId);
            Assert.AreEqual("bar", bar);
            await Task.CompletedTask;
            return new HttpStringDataSource("test");
        }
    }
}