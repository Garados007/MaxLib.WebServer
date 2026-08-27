using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    public abstract class EventConnection : WebSocketConnection
    {
        static readonly EventId EventParseErrorId = new(0, "event parse error");

        public EventFactory EventFactory { get; }

        protected EventConnection(Stream networkStream, EventFactory factory)
            : base(networkStream)
        {
            EventFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        protected override async Task ReceivedFrame(Frame frame)
        {
            EventBase? @event;
            try
            {
                @event = EventFactory.Parse(frame);
            }
            catch (MalformedEventJsonException e)
            {
                await ReceivedMalformedEvent(frame, e).ConfigureAwait(false);
                return;
            }
            catch (UnknownEventTypeException e)
            {
                await ReceivedUnknownEvent(frame, e).ConfigureAwait(false);
                return;
            }
            catch (InvalidEventPayloadException e)
            {
                await ReceivedInvalidEventPayload(frame, e).ConfigureAwait(false);
                return;
            }
            if (@event != null)
                await ReceivedFrame(@event).ConfigureAwait(false);
        }

        protected abstract Task ReceivedFrame(EventBase @event);

        /// <summary>
        /// Called when an incoming frame's payload was not syntactically valid JSON. The default
        /// implementation only logs the error; override to e.g. send an error event back to the
        /// client or close the connection.
        /// </summary>
        protected virtual Task ReceivedMalformedEvent(Frame frame, MalformedEventJsonException exception)
        {
            _ = frame ?? throw new ArgumentNullException(nameof(frame));
            _ = exception ?? throw new ArgumentNullException(nameof(exception));
            WebServerLog.LoggerFactory.CreateLogger(GetType())
                .LogError(EventParseErrorId, exception, "Received a WebSocket frame with malformed JSON");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when an incoming frame's "$type" discriminator is missing or does not match any
        /// event type registered on <see cref="EventFactory" />. The default implementation only
        /// logs the error; override to e.g. send an error event back to the client or close the
        /// connection.
        /// </summary>
        protected virtual Task ReceivedUnknownEvent(Frame frame, UnknownEventTypeException exception)
        {
            _ = frame ?? throw new ArgumentNullException(nameof(frame));
            _ = exception ?? throw new ArgumentNullException(nameof(exception));
            WebServerLog.LoggerFactory.CreateLogger(GetType())
                .LogError(EventParseErrorId, exception, "Received a WebSocket frame for an unknown event type {EventTypeName}",
                    exception.EventTypeName);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when an incoming frame named a registered event type via its "$type"
        /// discriminator, but its payload could not be mapped onto that type. The default
        /// implementation only logs the error; override to e.g. send an error event back to the
        /// client or close the connection.
        /// </summary>
        protected virtual Task ReceivedInvalidEventPayload(Frame frame, InvalidEventPayloadException exception)
        {
            _ = frame ?? throw new ArgumentNullException(nameof(frame));
            _ = exception ?? throw new ArgumentNullException(nameof(exception));
            WebServerLog.LoggerFactory.CreateLogger(GetType())
                .LogError(EventParseErrorId, exception, "Received an invalid payload for event type {EventTypeName}",
                    exception.EventTypeName);
            return Task.CompletedTask;
        }

        protected virtual async Task SendFrame(EventBase @event)
        {
            ArgumentNullException.ThrowIfNull(@event);
            var frame = EventFactory.ToFrame(@event);
            if (frame != null)
                await SendFrame(frame).ConfigureAwait(false);
        }
    }
}
