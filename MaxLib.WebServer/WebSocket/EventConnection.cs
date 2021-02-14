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
                await ReceivedFrame(@event);
        }

        protected abstract Task ReceivedFrame(EventBase @event);

        protected virtual async Task SendFrame(EventBase @event)
        {
            await SendFrame(@event.ToFrame());
        }
    }
}
