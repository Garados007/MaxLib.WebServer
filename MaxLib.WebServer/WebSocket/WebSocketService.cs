﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    public class WebSocketService : WebService, IDisposable, IAsyncDisposable
    {
        public WebSocketService() 
            : base(ServerStage.ParseRequest)
        {
        }

        public ICollection<IWebSocketEndpoint> Endpoints { get; } 
            = new List<IWebSocketEndpoint>();

        public WebSocketCloserEndpoint? CloseEndpoint { get; set; }

        public void Add<T>(WebSocketEndpoint<T> endpoint)
            where T : WebSocketConnection
        {
            Endpoints.Add(endpoint ?? throw new ArgumentNullException(nameof(endpoint)));
        }

        public override bool CanWorkWith(WebProgressTask task)
        {
            return task.Request.GetHeader("Upgrade") == "websocket" && 
                (task.Request.GetHeader("Connection")?.ToLower().Contains("upgrade") ?? false);
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (var endpoint in Endpoints)
                endpoint.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await Task.WhenAll(
                Endpoints.Select(async x => await x.DisposeAsync().ConfigureAwait(false))
            ).ConfigureAwait(false);
        }

        public override async Task ProgressTask(WebProgressTask task)
        {
            if (task.NetworkStream == null)
                return;

            var protocols = (task.Request.GetHeader("Sec-WebSocket-Protocol")?.ToLower() ?? "")
                .Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            var key = task.Request.GetHeader("Sec-WebSocket-Key");
            var version = task.Request.GetHeader("Sec-WebSocket-Version"); // MUST be 13 according RFC 6455

            if (key == null || version != "13")
            {
                task.Response.StatusCode = HttpStateCode.BadRequest;
                task.Response.SetHeader("Sec-WebSocket-Version", "13");
                task.NextStage = ServerStage.CreateResponse;
                return;
            }

            var responseKey = Convert.ToBase64String(
                System.Security.Cryptography.SHA1.Create().ComputeHash(
                    Encoding.UTF8.GetBytes(
                        $"{key.Trim()}258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                    )
                )
            );


            foreach (var endpoint in Endpoints)
            {
                if (protocols.Length > 0 && (endpoint.Protocol == null || !protocols.Contains(endpoint.Protocol)))
                {
                    continue;
                }

                var connection = await endpoint.Create(task.NetworkStream, task.Request).ConfigureAwait(false);
                if (connection == null)
                    continue;

                HandleCreateConnection(task, responseKey, endpoint, connection);
                return;
            }

            if (CloseEndpoint is WebSocketCloserEndpoint ep)
            {
                var connection = await ep.Create(task.NetworkStream, task.Request).ConfigureAwait(false);
                if (connection == null)
                    return;
                HandleCreateConnection(task, responseKey, ep, connection);
            }
        }

        private void HandleCreateConnection(WebProgressTask task, string responseKey, 
            IWebSocketEndpoint endpoint, WebSocketConnection connection)
        {
            task.Response.StatusCode = HttpStateCode.SwitchingProtocols;
            task.Response.SetHeader(
                ("Access-Control-Allow-Origin", "*"),
                ("Upgrade", "websocket"),
                ("Connection", "Upgrade"),
                ("Sec-WebSocket-Accept", responseKey),
                ("Sec-WebSocket-Protocol", endpoint.Protocol)
            );

            task.SwitchProtocols(async () =>
            {
                if (System.Diagnostics.Debugger.IsAttached)
                    await connection.HandshakeFinished().ConfigureAwait(false);
                else
                    try
                    {
                        await connection.HandshakeFinished().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        WebServerLog.Add(ServerLogType.Error, GetType(), "handshake", $"handshake error: {e}");
                    }
            });
            task.NextStage = ServerStage.SendResponse;
        }
    }
}
