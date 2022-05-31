using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    public interface IWebSocketEndpoint : IDisposable, IAsyncDisposable
    {
        Task<WebSocketConnection?> Create(Stream stream, HttpRequestHeader header);

        /// <summary>
        /// The Protocol of this endpoint. If the client sends a selection of protocols with
        /// <c>Sec-WebSocket-Protocol</c> than this protocol must be a member of it to select
        /// this endpoint. If this property is null the clients is required to send an empty
        /// list (or doesn't send the <c>Sec-WebSocket-Protocol</c> header at all) to select
        /// this endpoint.
        /// </summary>
        string? Protocol { get; }
    }

    public abstract class WebSocketEndpoint<T> : IWebSocketEndpoint
        where T : WebSocketConnection
    {
        readonly List<T> connections = new List<T>();
        readonly SemaphoreSlim connectionLock = new SemaphoreSlim(1, 1);

        public abstract string? Protocol { get; }

        public void Dispose()
        {
            connectionLock.Wait();
            foreach (var connection in connections)
                connection.Dispose();
            connections.Clear();
            connectionLock.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await connectionLock.WaitAsync().ConfigureAwait(false);
            foreach (var connection in connections)
                await connection.DisposeAsync().ConfigureAwait(false);
            connections.Clear();
            connectionLock.Dispose();
        }

        public async Task<WebSocketConnection?> Create(Stream stream, HttpRequestHeader header)
        {
            var connection = CreateConnection(stream, header);
            if (connection == null)
                return null;
            await connectionLock.WaitAsync().ConfigureAwait(false);
            connections.Add(connection);
            connectionLock.Release();
            connection.Closed += Connection_Closed;
            return connection;
        }

        private void Connection_Closed(object? sender, EventArgs eventArgs)
        {
            if (sender is T connection)
            {
                _ = RemoveConnection(connection);
            }
        }

        protected abstract T? CreateConnection(Stream stream, HttpRequestHeader header);

        public async Task RemoveConnection(T connection)
        {
            await connectionLock.WaitAsync().ConfigureAwait(false);
            connections.Remove(connection);
            connectionLock.Release();
            connection.Closed -= Connection_Closed;
        }
    }
}
