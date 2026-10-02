using MaxLib.WebServer.WebSocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;
using System.Text.Json;

namespace MaxLib.WebServer.Test.WebSocket
{
    [TestClass]
    public class EventSerialization
    {
        class PingEvent : EventBase
        {
            public int Sequence { get; set; }
        }

        class ChatEvent : EventBase
        {
            public string Message { get; set; } = "";
        }

        class NoParameterlessCtorEvent : EventBase
        {
            public NoParameterlessCtorEvent(int _) { }
        }

        static Frame FrameFromJson(string json)
            => new()
            {
                OpCode = OpCode.Text,
                Payload = Encoding.UTF8.GetBytes(json),
                FinalFrame = true,
            };

        [TestMethod]
        public void RoundTripTwoRegisteredEventTypes()
        {
            var factory = new EventFactory();
            factory.Add<PingEvent>();
            factory.Add<ChatEvent>();

            var pingFrame = factory.ToFrame(new PingEvent { Sequence = 42 });
            Assert.IsNotNull(pingFrame);
            var pingJson = Encoding.UTF8.GetString(pingFrame!.Payload.Span);
            StringAssert.Contains(pingJson, "\"$type\":\"PingEvent\"");

            var chatFrame = factory.ToFrame(new ChatEvent { Message = "hi" });
            Assert.IsNotNull(chatFrame);
            var chatJson = Encoding.UTF8.GetString(chatFrame!.Payload.Span);
            StringAssert.Contains(chatJson, "\"$type\":\"ChatEvent\"");

            Assert.IsTrue(factory.TryParse(pingFrame, out var pingResult));
            var ping = Assert.IsInstanceOfType<PingEvent>(pingResult);
            Assert.AreEqual(42, ping.Sequence);

            Assert.IsTrue(factory.TryParse(chatFrame, out var chatResult));
            var chat = Assert.IsInstanceOfType<ChatEvent>(chatResult);
            Assert.AreEqual("hi", chat.Message);
        }

        [TestMethod]
        public void AddWithExplicitKeyUsesThatKeyOnWire()
        {
            var factory = new EventFactory();
            factory.Add<PingEvent>("custom.ping");

            var frame = factory.ToFrame(new PingEvent { Sequence = 1 });
            Assert.IsNotNull(frame);
            var json = Encoding.UTF8.GetString(frame!.Payload.Span);
            StringAssert.Contains(json, "\"$type\":\"custom.ping\"");

            Assert.IsTrue(factory.TryParse(frame, out var result));
            Assert.IsInstanceOfType<PingEvent>(result);
        }

        [TestMethod]
        public void AddViaTypeOverloadValidatesConstructor()
        {
            var factory = new EventFactory();
            Assert.ThrowsExactly<ArgumentException>(
                () => factory.Add("x", typeof(NoParameterlessCtorEvent)));
            Assert.ThrowsExactly<ArgumentException>(
                () => factory.Add("y", typeof(string)));
        }

        [TestMethod]
        public void ParseUnregisteredTypeThrowsUnknownEventTypeException()
        {
            var factory = new EventFactory();
            factory.Add<PingEvent>();
            var frame = FrameFromJson("{\"$type\":\"NotRegistered\"}");

            var e = Assert.ThrowsExactly<UnknownEventTypeException>(() => factory.Parse(frame));
            Assert.AreEqual("NotRegistered", e.EventTypeName);
        }

        [TestMethod]
        public void ParseMissingTypeDiscriminatorThrowsUnknownEventTypeException()
        {
            var factory = new EventFactory();
            factory.Add<PingEvent>();
            var frame = FrameFromJson("{\"sequence\":1}");

            var e = Assert.ThrowsExactly<UnknownEventTypeException>(() => factory.Parse(frame));
            Assert.IsNull(e.EventTypeName);
        }

        [TestMethod]
        public void ParseMalformedJsonThrowsMalformedEventJsonException()
        {
            var factory = new EventFactory();
            factory.Add<PingEvent>();
            var frame = FrameFromJson("{ this is not json");

            Assert.ThrowsExactly<MalformedEventJsonException>(() => factory.Parse(frame));
        }

        [TestMethod]
        public void ParseAcceptsAPayloadWhereTypeDiscriminatorIsNotTheFirstProperty()
        {
            // "$type" need not be the first property; JSON is unordered (RFC 8259)
            var factory = new EventFactory();
            factory.Add<PingEvent>();
            var frame = FrameFromJson("{\"Sequence\":5,\"$type\":\"PingEvent\"}");

            var result = factory.Parse(frame);

            var ping = Assert.IsInstanceOfType<PingEvent>(result);
            Assert.AreEqual(5, ping.Sequence);
        }

        [TestMethod]
        public void ParseHonorsSeedOptionsAllowTrailingCommasOnTheTypeDiscriminatorPreScan()
        {
            // the "$type" pre-scan must be as lenient as the caller's seedOptions
            var seed = new JsonSerializerOptions { AllowTrailingCommas = true };
            var factory = new EventFactory(seed);
            factory.Add<PingEvent>();
            var frame = FrameFromJson("{\"$type\":\"PingEvent\",\"Sequence\":5,}");

            var result = factory.Parse(frame);

            var ping = Assert.IsInstanceOfType<PingEvent>(result);
            Assert.AreEqual(5, ping.Sequence);
        }

        [TestMethod]
        public void ParseHonorsSeedOptionsReadCommentHandlingOnTheTypeDiscriminatorPreScan()
        {
            var seed = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
            var factory = new EventFactory(seed);
            factory.Add<PingEvent>();
            var frame = FrameFromJson("{\"$type\":\"PingEvent\", // comment\n\"Sequence\":5}");

            var result = factory.Parse(frame);

            var ping = Assert.IsInstanceOfType<PingEvent>(result);
            Assert.AreEqual(5, ping.Sequence);
        }

#if NET10_0_OR_GREATER
        [TestMethod]
        public void ParseHonorsSeedOptionsAllowDuplicatePropertiesOnTheTypeDiscriminatorPreScan()
        {
            // AllowDuplicateProperties (net10.0+ only) is another leniency setting the pre-scan must honor
            var seed = new JsonSerializerOptions { AllowDuplicateProperties = false };
            var factory = new EventFactory(seed);
            factory.Add<PingEvent>();
            var frame = FrameFromJson("{\"$type\":\"PingEvent\",\"$type\":\"PingEvent\",\"Sequence\":5}");

            Assert.ThrowsExactly<MalformedEventJsonException>(() => factory.Parse(frame));
        }
#endif

        [TestMethod]
        public void ParseValidTypeWithBadPayloadThrowsInvalidEventPayloadException()
        {
            var factory = new EventFactory();
            factory.Add<PingEvent>();
            var frame = FrameFromJson("{\"$type\":\"PingEvent\",\"Sequence\":\"not-a-number\"}");

            var e = Assert.ThrowsExactly<InvalidEventPayloadException>(() => factory.Parse(frame));
            Assert.AreEqual("PingEvent", e.EventTypeName);
        }

        [TestMethod]
        public void TryParseUnregisteredTypeReturnsFalse()
        {
            var factory = new EventFactory();
            factory.Add<PingEvent>();
            var frame = FrameFromJson("{\"$type\":\"NotRegistered\"}");

            Assert.IsFalse(factory.TryParse(frame, out var @event));
            Assert.IsNull(@event);
        }

        [TestMethod]
        public void TryParseMalformedJsonReturnsFalse()
        {
            var factory = new EventFactory();
            factory.Add<PingEvent>();
            var frame = FrameFromJson("{ this is not json");

            Assert.IsFalse(factory.TryParse(frame, out var @event));
            Assert.IsNull(@event);
        }

        [TestMethod]
        public void AddAfterFirstUseThrows()
        {
            var factory = new EventFactory();
            factory.Add<PingEvent>();
            factory.ToFrame(new PingEvent());

            Assert.ThrowsExactly<InvalidOperationException>(() => factory.Add<ChatEvent>());
        }

        [TestMethod]
        public void AddAfterAFailedParseStillThrows()
        {
            // the registry must be sealed even when the first use is a Parse call that throws
            var factory = new EventFactory();
            factory.Add<PingEvent>();
            var frame = FrameFromJson("{\"$type\":\"NotRegistered\"}");

            Assert.ThrowsExactly<UnknownEventTypeException>(() => factory.Parse(frame));

            Assert.ThrowsExactly<InvalidOperationException>(() => factory.Add<ChatEvent>());
        }

        [TestMethod]
        public void MultipleFactoriesHaveIndependentRegistries()
        {
            var factoryA = new EventFactory();
            factoryA.Add<PingEvent>("shared");
            var factoryB = new EventFactory();
            factoryB.Add<ChatEvent>("shared");

            var frameFromA = factoryA.ToFrame(new PingEvent { Sequence = 7 });
            Assert.IsNotNull(frameFromA);
            var frameFromB = factoryB.ToFrame(new ChatEvent { Message = "hey" });
            Assert.IsNotNull(frameFromB);

            Assert.IsTrue(factoryA.TryParse(frameFromA!, out var resultA));
            Assert.IsInstanceOfType<PingEvent>(resultA);

            Assert.IsTrue(factoryB.TryParse(frameFromB!, out var resultB));
            Assert.IsInstanceOfType<ChatEvent>(resultB);
        }

        [TestMethod]
        public void SeedOptionsCarryForwardToWire()
        {
            var seed = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            var factory = new EventFactory(seed);
            factory.Add<PingEvent>();

            var frame = factory.ToFrame(new PingEvent { Sequence = 5 });
            Assert.IsNotNull(frame);
            var json = Encoding.UTF8.GetString(frame!.Payload.Span);
            StringAssert.Contains(json, "\"$type\":\"PingEvent\"");
            StringAssert.Contains(json, "\"sequence\":5");
        }
    }
}
