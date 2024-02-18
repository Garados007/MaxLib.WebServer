using System;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    [Serializable]
    public class TooLargePayloadException : Exception
    {
        public TooLargePayloadException() { }
        public TooLargePayloadException(string message) : base(message) { }
        public TooLargePayloadException(string message, Exception inner) : base(message, inner) { }
    }
}
