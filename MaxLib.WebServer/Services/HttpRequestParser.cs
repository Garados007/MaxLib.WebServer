using System.Threading;
using System.Text;
using System.IO;
using System;
using System.Globalization;
using System.Threading.Tasks;
using MaxLib.WebServer.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

#nullable enable

namespace MaxLib.WebServer.Services
{
    /// <summary>
    /// This <see cref="WebService" /> reads the request and put their data in the current
    /// <see cref="WebProgressTask" />.
    /// </summary>
    public class HttpRequestParser : WebService
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<HttpRequestParser>();
        static readonly EventId HeaderEventId = new(0, "Header");

        /// <summary>
        /// If this property is set to a file name this parser will write
        /// the content of each request to the request file. This file contains
        /// the request time, the full HTTP header and full POST content.
        /// <br />
        /// If either this or <see cref="DebugLogConnectionFile" /> is set then
        /// this parser will handle all requests synchronously (only one request
        /// is at the same time parsing).
        /// <br />
        /// Do not use this in production!
        /// </summary>
        public string? DebugWriteRequestFile { get; set; }

        /// <summary>
        /// If this property is set to a file name this parser will writer
        /// a brief description of each request to the connection file.
        /// This file contains only the request time, the remote IP and port,
        /// the requested host and url path.
        /// <br />
        /// If either this or <see cref="DebugWriteRequestFile" /> is set then
        /// this parser will handle all requests synchronously (only one request
        /// is at the same time parsing).
        /// <br />
        /// Do not use this in production!
        /// </summary>
        public string? DebugLogConnectionFile { get; set; }

        /// <summary>
        /// Sometimes the data is not available at instant. This can happen with slow
        /// connections. Therefore this instance will wait a maximum time until
        /// the first data is available. Negative or zero time values will disable
        /// this behavior.
        /// </summary>
        public TimeSpan MaxConnectionDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The maximum time the request-line and header-reading phase is given to complete as a
        /// whole, measured from when this parser starts reading (right after
        /// <see cref="WaitForData" /> succeeds) - not a per-line timeout. Without this, a client
        /// that trickles its request one byte (or one header line) at a time - the classic
        /// "Slowloris" attack - could hold the connection open indefinitely once <see
        /// cref="MaxConnectionDelay" />'s wait for the very first byte had already been
        /// satisfied. Set this to zero or a negative value to disable this timeout entirely.
        /// Default is 10 seconds.
        /// </summary>
        public TimeSpan MaxHeaderReadTime { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The maximum length the request method, url and http type combined are allowed to be. If
        /// this value exceeds this limit the parsing will be canceled and a <see
        /// cref="HttpStateCode.RequestUrlTooLong" /> will be returned. Set this a negative value to
        /// disable this behavior. Default is 1 MB (1 000 000 byte). <br/>
        /// </summary>
        public long MaxUrlLength { get; set; } = 1_000_000; // 1 MB

        /// <summary>
        /// The maximum length all header combined are allowed to be. If the requested header exceed
        /// this limit the parsing will be canceled and a <see
        /// cref="HttpStateCode.RequestHeaderFieldsTooLarge" /> will be returned. Set this to a
        /// negative value to disable this behavior. Default is 1 MB (1 000 000 byte).
        /// </summary>
        public long MaxHeaderLength { get; set; } = 1_000_000; // 1 MB

        /// <summary>
        /// The maximum request body size (in byte) that is automatically accepted without
        /// consulting <see cref="ContentLengthLimitExceeded" />. A request whose declared
        /// <c>Content-Length</c> exceeds this value triggers that callback (if one is registered)
        /// to obtain a possibly higher limit for this specific request; if no callback is
        /// registered, or the limit it returns is still exceeded, the request is rejected with
        /// <see cref="HttpStateCode.RequestEntityTooLarge" /> and its connection is closed (the
        /// client's body is never read, so the connection can't safely be reused). Set this to a
        /// negative value to disable this base limit. Default is 100 MB (100 000 000 byte).
        /// </summary>
        public long MaxContentLength { get; set; } = 100_000_000; // 100 MB

        /// <summary>
        /// Called when an incoming request's declared <c>Content-Length</c> (the second argument)
        /// exceeds <see cref="MaxContentLength" />. Return the content length limit to apply for
        /// this specific request instead — or <c>null</c> to allow any size. If this is not set,
        /// any request exceeding <see cref="MaxContentLength" /> is rejected outright. This allows
        /// e.g. raising the limit for specific paths, users, or temporarily, without changing the
        /// default for every other request.
        /// </summary>
        public Func<WebProgressTask, long, ValueTask<long?>>? ContentLengthLimitExceeded { get; set; }

        /// <summary>
        /// The fixed base component ("x") of the read timeout applied while receiving a request's
        /// body. The full timeout is this value plus a size-dependent component derived from
        /// <see cref="MinimumContentTransferRate" /> — see there for details. Unlike <see
        /// cref="MaxContentLength" />, this value applies uniformly to every request and cannot be
        /// overridden per-request. Default is 5 seconds.
        /// </summary>
        public TimeSpan ContentReadBaseTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The minimum transmission speed (in byte/s) a client is assumed to sustain while sending
        /// a request body. Used to derive the size-dependent component ("y") of the body read
        /// timeout: <c>y = Content-Length / MinimumContentTransferRate</c>. The full timeout for a
        /// request is <see cref="ContentReadBaseTimeout" /> + y, so a larger declared body is given
        /// proportionally more time to arrive, while a client that stalls mid-upload is still
        /// bounded rather than blocking the reader forever. Set this to zero or a negative value to
        /// disable the size-dependent component (every request then only gets <see
        /// cref="ContentReadBaseTimeout" />). Unlike <see cref="MaxContentLength" />, this value
        /// applies uniformly to every request and cannot be overridden per-request. Default is
        /// 16 000 byte/s (16 kB/s).
        /// </summary>
        public double MinimumContentTransferRate { get; set; } = 16_000; // 16 kB/s

        /// <summary>
        /// This <see cref="WebService" /> reads the request and put their data in the current
        /// <see cref="WebProgressTask" />.
        /// </summary>
        public HttpRequestParser()
            : base(ServerStage.ReadRequest)
        {
        }

        public override bool CanWorkWith(WebProgressTask task)
            => true;

        private readonly SemaphoreSlim debugSemaphore = new SemaphoreSlim(1, 1);

        private async ValueTask<StringBuilder?> DebugStartRequest()
        {
            if (DebugLogConnectionFile == null && DebugWriteRequestFile == null)
                return null;

            // enter locked debug zone
            await debugSemaphore.WaitAsync().ConfigureAwait(false);

            if (DebugWriteRequestFile != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine(new string('=', 100));
                sb.AppendLine(CultureInfo.InvariantCulture, $"=   {WebServerUtils.GetDateString(DateTime.UtcNow).PadRight(95, ' ')}=");
                sb.AppendLine(new string('=', 100));
                sb.AppendLine();
                return sb;
            }
            else return null;
        }

        private async ValueTask DebugFinishRequest(StringBuilder? debugBuilder)
        {
            if (DebugLogConnectionFile == null && DebugWriteRequestFile == null)
                return;

            if (debugBuilder != null)
            {
                debugBuilder.AppendLine();
                debugBuilder.AppendLine();
                await File.AppendAllTextAsync(DebugWriteRequestFile!, debugBuilder.ToString()).ConfigureAwait(false);
            }

            // release locked debug zone
            debugSemaphore.Release();
        }

        private async ValueTask DebugConnection(WebProgressTask task)
        {
            if (DebugLogConnectionFile == null)
                return;

            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"{WebServerUtils.GetDateString(DateTime.UtcNow)} " +
                $"{task.Connection?.NetworkClient?.Client.RemoteEndPoint}");
            var host = task.Request.HeaderParameter.TryGetValue("Host", out string? host_)
                ? host_ : "";
            sb.AppendLine("    " + host + task.Request.Location.DocumentPath);
            sb.AppendLine();

            await File.AppendAllTextAsync(DebugLogConnectionFile, sb.ToString()).ConfigureAwait(false);
        }

        protected virtual async ValueTask<bool> WaitForData(WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            try
            {
                if (task.NetworkStream is NetworkStream ns && !ns.DataAvailable)
                {
                    var maxDelay = MaxConnectionDelay;
                    var maxSlice = TimeSpan.FromMilliseconds(10);
                    while (maxDelay > TimeSpan.Zero && !ns.DataAvailable)
                    {
                        var slice = maxSlice < maxDelay ? maxSlice : maxDelay;
                        await Task.Delay(slice).ConfigureAwait(false);
                        maxDelay -= slice;
                    }
                    if (!ns.DataAvailable)
                    {
                        logger.LogError(HeaderEventId, "Request Timeout");
                        task.Request.FieldConnection = HttpConnectionType.KeepAlive;
                        task.Response.StatusCode = HttpStateCode.RequestTimeOut;
                        task.NextStage = ServerStage.CreateResponse;
                        return false;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                logger.LogError(HeaderEventId, "Connection closed by remote host");
                task.Response.StatusCode = HttpStateCode.RequestTimeOut;
                task.NextStage = ServerStage.FINAL_STAGE;
                return false;
            }
            return true;
        }

        protected virtual async ValueTask<string?> ReadLine(WebProgressTask task,
            NetworkReader reader, long limit, HttpStateCode exceedState,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(reader);
            string? line;
            try { line = await reader.ReadLineAsync(limit, cancellationToken).ConfigureAwait(false); }
            catch (IO.ReadLineOverflowException e)
            {
                e.State = exceedState;
                throw;
            }
            catch (OperationCanceledException)
            {
                // unlike the generic catch below, the connection itself is still fine here -
                // only the wait timed out - so still attempt a response on it, same as
                // WaitForData's own timeout; but its remaining bytes (if the client is still
                // sending more) are now abandoned mid-stream, so it can't safely be reused
                logger.LogError(HeaderEventId, "Timed out while reading the request header");
                task.Response.StatusCode = HttpStateCode.RequestTimeOut;
                task.Request.FieldConnection = HttpConnectionType.Close;
                task.NextStage = ServerStage.CreateResponse;
                return null;
            }
            catch
            {
                logger.LogError(HeaderEventId, "Connection closed by remote host");
                task.Response.StatusCode = HttpStateCode.RequestTimeOut;
                task.NextStage = ServerStage.FINAL_STAGE;
                return null;
            }
            if (line == null)
            {
                logger.LogError(HeaderEventId, "Can't read Header line");
                task.Response.StatusCode = HttpStateCode.BadRequest;
                task.NextStage = ServerStage.CreateResponse;
            }
            return line;
        }

        protected virtual bool ParseFirstHeaderLine(WebProgressTask task, string line)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(line);
            logger.LogDebug(HeaderEventId, "{Line}", line);
            var parts = line.Split(' ');
            if (parts.Length != 3)
            {
                logger.LogError(HeaderEventId, "Bad Request");
                task.Response.StatusCode = HttpStateCode.BadRequest;
                task.NextStage = ServerStage.CreateResponse;
                return false;
            }

            task.Request.ProtocolMethod = parts[0];
            task.Request.Url = parts[1];
            task.Request.HttpProtocol = parts[2];

            return true;
        }

        protected virtual bool ParseOtherHeaderLine(WebProgressTask task, string line)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(line);
            var ind = line.IndexOf(':', StringComparison.Ordinal);
            if (ind < 0)
            {
                logger.LogError(HeaderEventId, "Bad Request");
                task.Response.StatusCode = HttpStateCode.BadRequest;
                task.NextStage = ServerStage.CreateResponse;
                return false;
            }

            var key = line[..ind].Trim();
            var value = line[(ind + 1)..].Trim();
            // RFC 7230 §3.3.3: differing Content-Length values must be rejected (CL.CL request smuggling);
            // identical repeated values are fine
            if (string.Equals(key, "Content-Length", StringComparison.OrdinalIgnoreCase) &&
                task.Request.HeaderParameter.TryGetValue(key, out var existingLength) &&
                existingLength != value)
            {
                logger.LogError(HeaderEventId, "Bad Request, conflicting Content-Length headers");
                task.Response.StatusCode = HttpStateCode.BadRequest;
                task.NextStage = ServerStage.CreateResponse;
                // the client's framing of the body is now ambiguous - keeping the connection
                // alive risks misparsing leftover bytes as the header of the next request
                task.Request.FieldConnection = HttpConnectionType.Close;
                return false;
            }
            // RFC 7230 §5.4: more than one Host header is always rejected, even with identical values
            if (string.Equals(key, "Host", StringComparison.OrdinalIgnoreCase) &&
                task.Request.HeaderParameter.ContainsKey(key))
            {
                logger.LogError(HeaderEventId, "Bad Request, duplicate Host header");
                task.Response.StatusCode = HttpStateCode.BadRequest;
                task.NextStage = ServerStage.CreateResponse;
                task.Request.FieldConnection = HttpConnectionType.Close;
                return false;
            }
            // last-wins on any other repeated header name (Dictionary.Add would throw)
            task.Request.HeaderParameter[key] = value;

            return true;
        }

        protected virtual async ValueTask<bool> LoadContent(WebProgressTask task, NetworkReader reader)
        {
            ArgumentNullException.ThrowIfNull(task);

            // Transfer-Encoding request bodies are not supported (there is no chunked
            // decoder), and honoring Content-Length while ignoring Transfer-Encoding (or
            // vice versa) enables request smuggling against a front-end proxy that
            // interprets the two differently (RFC 7230 §3.3.3). Reject outright until
            // chunked request decoding is implemented; this also covers a request that
            // sends both headers at once.
            if (task.Request.HeaderParameter.ContainsKey("Transfer-Encoding"))
            {
                logger.LogError(HeaderEventId, "Transfer-Encoding is not supported");
                task.Response.StatusCode = HttpStateCode.NotImplemented;
                task.NextStage = ServerStage.CreateResponse;
                // the request's body (if any) is left unread on the socket; keeping the
                // connection alive would let those bytes be parsed as the header of the
                // next request, so force the connection closed after this response
                task.Request.FieldConnection = HttpConnectionType.Close;
                return false;
            }

            if (!task.Request.HeaderParameter.TryGetValue("Content-Length", out string? strLength))
                return true;

            if (!long.TryParse(strLength, out long length) || length < 0)
            {
                logger.LogError(HeaderEventId, "Bad Request, invalid content length");
                task.Response.StatusCode = HttpStateCode.BadRequest;
                task.NextStage = ServerStage.CreateResponse;
                return false;
            }

            if (MaxContentLength >= 0 && length > MaxContentLength)
            {
                var allowedLength = ContentLengthLimitExceeded != null
                    ? await ContentLengthLimitExceeded(task, length).ConfigureAwait(false)
                    : MaxContentLength;
                // allowedLength == null means the callback lifted the limit entirely
                if (allowedLength != null && length > allowedLength.Value)
                {
                    logger.LogError(HeaderEventId, "Request Entity Too Large");
                    task.Response.StatusCode = HttpStateCode.RequestEntityTooLarge;
                    task.NextStage = ServerStage.CreateResponse;
                    // the client's body is never read, so leftover bytes would corrupt
                    // the next request on a reused connection
                    task.Request.FieldConnection = HttpConnectionType.Close;
                    return false;
                }
            }

            // bound how long this request's body is given to arrive: a fixed base
            // component plus a size-dependent one, so a stalled client can't pin the
            // reader (and later, the drain-on-dispose) forever, regardless of how large
            // (but still accepted) its declared Content-Length is
            var timeout = ContentReadBaseTimeout + (MinimumContentTransferRate > 0
                ? TimeSpan.FromSeconds(length / MinimumContentTransferRate)
                : TimeSpan.Zero);

#pragma warning disable CA2000 // ownership transfers via SetPost into HttpPost.Content, disposed by HttpPost.Dispose()/DisposeAsync()
            var content = new IO.ContentStream(reader, length, timeout);

            task.Request.Post.SetPost(
                task,
                content,
                task.Request.HeaderParameter.TryGetValue("Content-Type", out string? contentType)
                    ? contentType : null
            );
#pragma warning restore CA2000

            return true;
        }

        public override async Task ProgressTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            _ = task.NetworkStream ?? throw new ArgumentNullException(nameof(task));

            // leaveOpen: true — this reader wraps the live connection stream, whose
            // lifetime is owned by the Server/HttpConnection (it is reused across
            // Keep-Alive requests). ContentStream now disposes the NetworkReader it
            // is given once the request body has been drained; without leaveOpen
            // that would close the connection stream after every request that had a
            // body, defeating Keep-Alive.
#pragma warning disable CA2000 // must not dispose: reader is captured by a lazily-read ContentStream and consumed after this method returns
            var reader = new NetworkReader(task.NetworkStream, leaveOpen: true);
#pragma warning restore CA2000
            StringBuilder? debugBuilder = null;

            try
            {
                debugBuilder = await DebugStartRequest().ConfigureAwait(false);

                // wait until some data is received.
                if (!await WaitForData(task).ConfigureAwait(false))
                    return;

                // bound the entire request-line + header-reading phase as a whole (not
                // per-line), so a client that trickles its request one byte at a time can't
                // hold the connection open indefinitely just because it satisfied
                // WaitForData's wait for the very first byte - see MaxHeaderReadTime
                using var headerTimeoutSource = MaxHeaderReadTime > TimeSpan.Zero
                    ? new CancellationTokenSource(MaxHeaderReadTime)
                    : null;
                var headerToken = headerTimeoutSource?.Token ?? CancellationToken.None;

                // read first header line
                var line = await ReadLine(task, reader, MaxUrlLength, HttpStateCode.RequestUrlTooLong, headerToken)
                    .ConfigureAwait(false);
                if (line == null)
                    return;
                debugBuilder?.AppendLine(line);
                if (!ParseFirstHeaderLine(task, line))
                    return;

                // read all other header lines
                var limit = MaxHeaderLength;
                while (!string.IsNullOrWhiteSpace(line = await ReadLine(task, reader, limit, HttpStateCode.RequestHeaderFieldsTooLarge, headerToken).ConfigureAwait(false)))
                {
                    debugBuilder?.AppendLine(line);
                    if (!ParseOtherHeaderLine(task, line))
                        return;
                    if (limit >= 0)
                    {
                        limit -= line.Length;
                        if (limit < 0)
                            throw new IO.ReadLineOverflowException(HttpStateCode.RequestHeaderFieldsTooLarge);
                    }

                }
                debugBuilder?.AppendLine();

                // read content if possible
                if (!await LoadContent(task, reader).ConfigureAwait(false))
                    return;

                await DebugConnection(task).ConfigureAwait(false);
            }
            catch (IO.ReadLineOverflowException e)
            {
                task.Response.StatusCode = e.State;
                task.NextStage = ServerStage.CreateResponse;
            }
            finally
            {
                await DebugFinishRequest(debugBuilder).ConfigureAwait(false);
            }
        }
    }
}
