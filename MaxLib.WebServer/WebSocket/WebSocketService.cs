using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.WebSocket
{
    public class WebSocketService : WebService, IDisposable, IAsyncDisposable
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<WebSocketService>();
        static readonly EventId HandshakeEventId = new(0, "handshake");

        private static readonly char[] ProtocolSeparators = [' ', ','];

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
            ArgumentNullException.ThrowIfNull(task);
            // RFC 6455 section 4.2.1: the Upgrade value is case-insensitive
            return string.Equals(task.Request.GetHeader("Upgrade"), "websocket", StringComparison.OrdinalIgnoreCase) &&
                (task.Request.GetHeader("Connection")?.Contains("upgrade", StringComparison.OrdinalIgnoreCase) ?? false);
        }

        public override void Dispose()
        {
            base.Dispose();
            GC.SuppressFinalize(this);
            foreach (var endpoint in Endpoints)
                endpoint.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await Task.WhenAll(
                Endpoints.Select(async x => await x.DisposeAsync().ConfigureAwait(false))
            ).ConfigureAwait(false);
        }

        public override async Task ProgressTask(WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            if (task.NetworkStream == null)
                return;

            var rawProtocols = (task.Request.GetHeader("Sec-WebSocket-Protocol") ?? "")
                .Split(ProtocolSeparators, StringSplitOptions.RemoveEmptyEntries);
            var protocols = Array.ConvertAll(rawProtocols, p => p.ToLowerInvariant());

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
                System.Security.Cryptography.SHA1.HashData(
                    Encoding.UTF8.GetBytes(
                        $"{key.Trim()}258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                    )
                )
            );


            foreach (var endpoint in Endpoints)
            {
                // symmetric with IWebSocketEndpoint.Protocol: a non-null Protocol requires the client to ask for it,
                // a null Protocol requires the client to ask for none
                string? matchedProtocol;
                if (endpoint.Protocol == null)
                {
                    if (protocols.Length > 0)
                        continue;
                    matchedProtocol = null;
                }
                else
                {
                    var idx = Array.IndexOf(protocols, endpoint.Protocol.ToLowerInvariant());
                    if (idx < 0)
                        continue;
                    // RFC 6455 §4.2.2: the response must be one of the client's offered tokens verbatim,
                    // not the endpoint's casing
                    matchedProtocol = rawProtocols[idx];
                }

                var connection = await endpoint.Create(task.NetworkStream, task.Request).ConfigureAwait(false);
                if (connection == null)
                    continue;

                HandleCreateConnection(task, responseKey, matchedProtocol, connection);
                return;
            }

            if (CloseEndpoint is WebSocketCloserEndpoint ep)
            {
                var connection = await ep.Create(task.NetworkStream, task.Request).ConfigureAwait(false);
                if (connection == null)
                    return;
                HandleCreateConnection(task, responseKey, ep.Protocol, connection);
            }
        }

        private static void HandleCreateConnection(WebProgressTask task, string responseKey,
            string? protocol, WebSocketConnection connection)
        {
            task.Response.StatusCode = HttpStateCode.SwitchingProtocols;
            task.Response.SetHeader(
                ("Access-Control-Allow-Origin", "*"),
                ("Upgrade", "websocket"),
                ("Connection", "Upgrade"),
                ("Sec-WebSocket-Accept", responseKey),
                ("Sec-WebSocket-Protocol", protocol)
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
                        logger.LogError(HandshakeEventId, e, "Handshake error");
                    }
            });
            task.NextStage = ServerStage.SendResponse;
        }
    }
}
