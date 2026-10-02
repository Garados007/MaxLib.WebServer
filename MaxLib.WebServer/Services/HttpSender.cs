using System;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Services
{
    /// <summary>
    /// WebServiceType.SendResponse: Sendet Response und Dokument, wenn vorhanden, an den Clienten.
    /// </summary>
    /// <remarks>
    /// Also discards any unread POST data once the response has been fully sent, readying the
    /// connection for the next request. See <see cref="HttpResponseCreator" /> for why this
    /// happens here and not earlier.
    /// </remarks>
    public class HttpSender : WebService
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<HttpSender>();
        static readonly EventId StatusCodeEventId = new(0, "StatusCode");
        static readonly EventId SendEventId = new(0, "Send");
        static readonly EventId DisposeEventId = new(0, "Dispose");

        /// <summary>
        /// WebServiceType.SendResponse: Sendet Response und Dokument, wenn vorhanden, an den Clienten.
        /// </summary>
        public HttpSender() : base(ServerStage.SendResponse) { }

        public virtual string StatusCodeText(HttpStateCode code)
        {
            switch ((int)code)
            {
                case 100: return "Continue";
                case 101: return "Switching Protcols";
                case 102: return "Processing";
                case 200: return "OK";
                case 201: return "Created";
                case 202: return "Accepted";
                case 203: return "Non-Authoritative Information";
                case 204: return "No Content";
                case 205: return "Reset Content";
                case 206: return "Partial Content";
                case 207: return "Multi-Status";
                case 208: return "IM Used";
                case 300: return "Multiple Choises";
                case 301: return "Moved Permanently";
                case 302: return "Found";
                case 303: return "See Other";
                case 304: return "Not Modified";
                case 305: return "Use Proxy";
                case 307: return "Temporary Redirect";
                case 308: return "Permanent Redirect";
                case 400: return "Bad Request";
                case 401: return "Unathorized";
                case 402: return "Payment Required";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 406: return "Not Acceptable";
                case 407: return "Proxy Authendtication Required";
                case 408: return "Request Time-out";
                case 409: return "Conflict";
                case 410: return "Gone";
                case 411: return "Length Required";
                case 412: return "Precondition Failed";
                case 413: return "Request Entity Too Large";
                case 414: return "Request-URL Too Long";
                case 415: return "Unsupported Media Type";
                case 416: return "Requested range not satisfiable";
                case 417: return "Expectation Failed";
                case 418: return "I'm a teapot";
                case 420: return "Policy Not Fulfilled";
                case 421: return "There are too many connections from your internet address";
                case 422: return "Unprocessable Entity";
                case 423: return "Locked";
                case 424: return "Failed Dependency";
                case 425: return "Unordered Collection";
                case 426: return "Upgrade Required";
                case 428: return "Precondition Required";
                case 429: return "Too Many Requests";
                case 431: return "Request Header Fields Too Large";
                case 500: return "Internal Server Error";
                case 501: return "Not Implemented";
                case 502: return "Bad Gateway";
                case 503: return "Service Unavailable";
                case 504: return "Gateway Time-out";
                case 505: return "HTTP Version not supported";
                case 506: return "Variant Also Negotiates";
                case 507: return "Insufficient Storage";
                case 508: return "Loop Detected";
                case 509: return "Bandwidth Limit Exceeded";
                case 510: return "Not Extended";
                default:
                    logger.LogInformation(StatusCodeEventId,
                        "Cant get status string from {StatusCode} ({StatusCodeNumber}).", code, (int)code);
                    return "";
            }
        }

        public override async Task ProgressTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));

            var header = task.Response;
            var stream = task.NetworkStream;
            if (stream == null)
                return;
            try
            {
#pragma warning disable CA2000 // must not dispose: would close the still-needed connection stream, and StreamWriter's default no-BOM encoding must not be swapped just to add leaveOpen
                var writer = new StreamWriter(stream);
#pragma warning restore CA2000
                await writer.WriteAsync(header.HttpProtocol).ConfigureAwait(false);
                await writer.WriteAsync(" ").ConfigureAwait(false);
                await writer.WriteAsync(((int)header.StatusCode).ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                await writer.WriteAsync(" ").ConfigureAwait(false);
                await writer.WriteLineAsync(StatusCodeText(header.StatusCode)).ConfigureAwait(false);
                for (int i = 0; i < header.HeaderParameter.Count; ++i) //Parameter
                {
                    var e = header.HeaderParameter.ElementAt(i);
                    await writer.WriteAsync(WebServerUtils.RemoveCrLf(e.Key)).ConfigureAwait(false);
                    await writer.WriteAsync(": ").ConfigureAwait(false);
                    await writer.WriteLineAsync(WebServerUtils.RemoveCrLf(e.Value)).ConfigureAwait(false);
                }
                foreach (var cookie in task.Request.Cookie.AddedCookies) //Cookies
                {
                    await writer.WriteAsync("Set-Cookie: ").ConfigureAwait(false);
                    await writer.WriteLineAsync(cookie.Value.ToString()).ConfigureAwait(false);
                }
                await writer.WriteLineAsync().ConfigureAwait(false);
                try { await writer.FlushAsync().ConfigureAwait(false); }
                catch (ObjectDisposedException)
                {
                    logger.LogError(SendEventId, "Connection closed by remote host.");
                    return;
                }
                catch (IOException)
                {
                    logger.LogError(SendEventId, "Connection closed by remote host.");
                    return;
                }
                //Daten senden
                if (!(task.Document.Information.ContainsKey("Only Header") && (bool)task.Document.Information["Only Header"]!))
                    for (int i = 0; i < task.Document.DataSources.Count; ++i)
                    {
                        await task.Document.DataSources[i].WriteStream(stream).ConfigureAwait(false);
                    }
                try { await stream.FlushAsync().ConfigureAwait(false); }
                catch (IOException)
                {
                    logger.LogError(SendEventId, "Connection closed by remote host.");
                    return;
                }
            }
            finally
            {
                // must run however the try block above exits: this is the only place the request's POST data
                // (and its temp files) is disposed
                await DisposeRequestPostAsync(task).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Disposes <paramref name="task" />'s request POST data (temp files included), logging and marking the
        /// connection for closure on failure. Every <c>SendResponse</c>-stage sender must call this exactly once,
        /// from a <c>finally</c> block around its response transmission.
        /// </summary>
        protected static async Task DisposeRequestPostAsync(WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            try
            {
                await task.Request.Post.DisposeAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning(DisposeEventId,
                    "Timed out while disposing request content; closing the connection");
                task.Request.FieldConnection = HttpConnectionType.Close;
            }
            catch (IOException)
            {
                // an ordinary connection reset while draining unread POST data: log it like the other
                // broken-connection paths instead of letting it reach Server.ProcessTask's catch-all
                logger.LogInformation(DisposeEventId, "Connection closed by remote host.");
                task.Request.FieldConnection = HttpConnectionType.Close;
            }
        }

        public override bool CanWorkWith(WebProgressTask task)
            => true;
    }
}
