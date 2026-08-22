using System;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    public abstract class EventConnection : WebSocketConnection
    {
        public EventFactory EventFactory { get; }

        protected EventConnection(Stream networkStream, EventFactory factory) 
            : base(networkStream)
        {
            EventFactory = factory;
        }

        protected override async Task ReceivedFrame(Frame frame)
        {
            if (EventFactory.TryParse(frame, out EventBase? @event))
                await ReceivedFrame(@event).ConfigureAwait(false);
        }

        protected abstract Task ReceivedFrame(EventBase @event);

        protected virtual async Task SendFrame(EventBase @event)
        {
            ArgumentNullException.ThrowIfNull(@event);
            var frame = EventFactory.ToFrame(@event);
            if (frame != null)
                await SendFrame(frame).ConfigureAwait(false);
        }
    }
}
