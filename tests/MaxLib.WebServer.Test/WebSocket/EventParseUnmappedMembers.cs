using MaxLib.WebServer.WebSocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace MaxLib.WebServer.Test.WebSocket
{
    [TestClass]
    public class EventParseUnmappedMembers
    {
        class PingEvent : EventBase
        {
            public int Sequence { get; set; }
        }

        [TestMethod]
        public void ParseRoundTripsWithUnmappedMemberHandlingDisallow()
        {
            var factory = new EventFactory(new JsonSerializerOptions
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            });
            factory.Add<PingEvent>();

            var frame = factory.ToFrame(new PingEvent { Sequence = 7 });
            Assert.IsNotNull(frame);

            var parsed = factory.Parse(frame!);
            var ping = Assert.IsInstanceOfType<PingEvent>(parsed);
            Assert.AreEqual(7, ping.Sequence);
        }
    }
}
