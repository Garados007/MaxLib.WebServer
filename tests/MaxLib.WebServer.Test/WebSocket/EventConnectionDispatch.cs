using MaxLib.WebServer.WebSocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Test.WebSocket
{
    [TestClass]
    public class EventConnectionDispatch
    {
        class PingEvent : EventBase
        {
            public int Sequence { get; set; }
        }

        class RecordingConnection : EventConnection
        {
            public readonly List<EventBase> ReceivedEvents = [];
            public MalformedEventJsonException? LastMalformedEvent;
            public UnknownEventTypeException? LastUnknownEvent;
            public InvalidEventPayloadException? LastInvalidPayload;

            public RecordingConnection(EventFactory factory)
                : base(new MemoryStream(), factory)
            {
            }

            public Task Dispatch(Frame frame)
                => ReceivedFrame(frame);

            protected override Task ReceiveClose(CloseReason? reason, string? info)
                => Task.CompletedTask;

            protected override Task ReceivedFrame(EventBase @event)
            {
                ReceivedEvents.Add(@event);
                return Task.CompletedTask;
            }

            protected override Task ReceivedMalformedEvent(Frame frame, MalformedEventJsonException exception)
            {
                LastMalformedEvent = exception;
                return Task.CompletedTask;
            }

            protected override Task ReceivedUnknownEvent(Frame frame, UnknownEventTypeException exception)
            {
                LastUnknownEvent = exception;
                return Task.CompletedTask;
            }

            protected override Task ReceivedInvalidEventPayload(Frame frame, InvalidEventPayloadException exception)
            {
                LastInvalidPayload = exception;
                return Task.CompletedTask;
            }
        }

        static Frame FrameFromJson(string json)
            => new()
            {
                OpCode = OpCode.Text,
                Payload = Encoding.UTF8.GetBytes(json),
                FinalFrame = true,
            };

        static RecordingConnection MakeConnection()
        {
            var factory = new EventFactory();
            factory.Add<PingEvent>();
            return new RecordingConnection(factory);
        }

        [TestMethod]
        public async Task ValidEventReachesReceivedFrame()
        {
            var connection = MakeConnection();

            await connection.Dispatch(FrameFromJson("{\"$type\":\"PingEvent\",\"Sequence\":7}"));

            var received = Assert.IsInstanceOfType<PingEvent>(Assert.ContainsSingle(connection.ReceivedEvents));
            Assert.AreEqual(7, received.Sequence);
            Assert.IsNull(connection.LastMalformedEvent);
            Assert.IsNull(connection.LastUnknownEvent);
            Assert.IsNull(connection.LastInvalidPayload);
        }

        [TestMethod]
        public async Task MalformedJsonRoutesToReceivedMalformedEvent()
        {
            var connection = MakeConnection();

            await connection.Dispatch(FrameFromJson("{ this is not json"));

            Assert.IsNotNull(connection.LastMalformedEvent);
            Assert.IsEmpty(connection.ReceivedEvents);
            Assert.IsNull(connection.LastUnknownEvent);
            Assert.IsNull(connection.LastInvalidPayload);
        }

        [TestMethod]
        public async Task UnregisteredEventRoutesToReceivedUnknownEvent()
        {
            var connection = MakeConnection();

            await connection.Dispatch(FrameFromJson("{\"$type\":\"NotRegistered\"}"));

            Assert.IsNotNull(connection.LastUnknownEvent);
            Assert.AreEqual("NotRegistered", connection.LastUnknownEvent!.EventTypeName);
            Assert.IsEmpty(connection.ReceivedEvents);
            Assert.IsNull(connection.LastMalformedEvent);
            Assert.IsNull(connection.LastInvalidPayload);
        }

        [TestMethod]
        public async Task BadPayloadForKnownTypeRoutesToReceivedInvalidEventPayload()
        {
            var connection = MakeConnection();

            await connection.Dispatch(FrameFromJson("{\"$type\":\"PingEvent\",\"Sequence\":\"not-a-number\"}"));

            Assert.IsNotNull(connection.LastInvalidPayload);
            Assert.AreEqual("PingEvent", connection.LastInvalidPayload!.EventTypeName);
            Assert.IsEmpty(connection.ReceivedEvents);
            Assert.IsNull(connection.LastMalformedEvent);
            Assert.IsNull(connection.LastUnknownEvent);
        }
    }
}
