using System.Threading;
using System.Text;
using System.IO;
using System;
using System.Threading.Tasks;
using MaxLib.WebServer.IO;
using System.Net.Sockets;

#nullable enable

namespace MaxLib.WebServer.Services
{
    /// <summary>
    /// This <see cref="WebService" /> reads the request and put their data in the current
    /// <see cref="WebProgressTask" />.
    /// </summary>
    public class HttpRequestParser : WebService
    {
        /// <summary>
        /// If this property is set to a file name this parser will write 
        /// the content of each request to the request file. This file contains
        /// the request time, the full HTTP header and full POST content.
        /// <br />
        /// If either this or <see cref="DebugLogConnectionFile" /> is set then
        /// this parser will handle all requests syncronously (only one request
        /// is at the same time parsing). 
        /// <br />
        /// Do not use this in production!
        /// </summary>
        public string? DebugWriteRequestFile { get; set; } = null;

        /// <summary>
        /// If this property is set to a file name this parser will writer
        /// a brief description of each request to the connection file. 
        /// This file contains only the request time, the remote IP and port,
        /// the requested host and url path.
        /// <br />
        /// If either this or <see cref="DebugWriteRequestFile" /> is set then
        /// this parser will handle all requests syncronously (only one request
        /// is at the same time parsing). 
        /// <br />
        /// Do not use this in production!
        /// </summary>
        public string? DebugLogConnectionFile { get; set; } = null;

        /// <summary>
        /// Sometimes the data is not avaible at instant. This can happen with slow
        /// connections. Therefore this instance will wait a maximum time until
        /// the first data is available. Negative or zero time values will disable
        /// this behaviour.
        /// </summary>
        public TimeSpan MaxConnectionDelay { get; set; } = TimeSpan.FromSeconds(5);

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
                sb.AppendLine($"=   {WebServerUtils.GetDateString(DateTime.UtcNow).PadRight(95, ' ')}=");
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
                await File.AppendAllTextAsync(DebugWriteRequestFile, debugBuilder.ToString()).ConfigureAwait(false);
            }

            // release locked debug zone
            debugSemaphore.Release();
        }

        private async ValueTask DebugConnection(WebProgressTask task)
        {
            if (DebugLogConnectionFile == null)
                return;
            
            var sb = new StringBuilder();
            sb.AppendLine($"{WebServerUtils.GetDateString(DateTime.UtcNow)} " +
                $"{task.Connection?.NetworkClient?.Client.RemoteEndPoint}");
            var host = task.Request.HeaderParameter.TryGetValue("Host", out string host_)
                ? host_ : "";
            sb.AppendLine("    " + host + task.Request.Location.DocumentPath);
            sb.AppendLine();
            
            await File.AppendAllTextAsync(DebugLogConnectionFile, sb.ToString()).ConfigureAwait(false);
        }

        protected virtual async ValueTask<bool> WaitForData(WebProgressTask task)
        {
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
                        WebServerLog.Add(ServerLogType.Error, GetType(), "Header", "Request Timeout");
                        task.Request.FieldConnection = HttpConnectionType.KeepAlive;
                        task.Response.StatusCode = HttpStateCode.RequestTimeOut;
                        task.NextStage = ServerStage.CreateResponse;
                        return false;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                WebServerLog.Add(ServerLogType.Error, GetType(), "Header", "Connection closed by remote host");
                task.Response.StatusCode = HttpStateCode.RequestTimeOut;
                task.NextStage = ServerStage.FINAL_STAGE;
                return false;
            }
            return true;
        }

        protected virtual async ValueTask<string?> ReadLine(WebProgressTask task, NetworkReader reader)
        {
            string? line;
            try { line = await reader.ReadLineAsync().ConfigureAwait(false); }
            catch
            {
                WebServerLog.Add(ServerLogType.Error, GetType(), "Header", "Connection closed by remote host");
                task.Response.StatusCode = HttpStateCode.RequestTimeOut;
                task.NextStage = ServerStage.FINAL_STAGE;
                return null;
            }
            if (line == null)
            {
                WebServerLog.Add(ServerLogType.Error, GetType(), "Header", "Can't read Header line");
                task.Response.StatusCode = HttpStateCode.BadRequest;
                task.NextStage = ServerStage.CreateResponse;
            }
            return line;
        }

        protected virtual bool ParseFirstHeaderLine(WebProgressTask task, string line)
        {
            WebServerLog.Add(ServerLogType.Debug, GetType(), "Header", line);
            var parts = line.Split(' ');
            if (parts.Length != 3)
            {
                WebServerLog.Add(ServerLogType.Error, GetType(), "Header", "Bad Request");
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
            var ind = line.IndexOf(':');
            if (ind < 0)
            {
                WebServerLog.Add(ServerLogType.Error, GetType(), "Header", "Bad Request");
                task.Response.StatusCode = HttpStateCode.BadRequest;
                task.NextStage = ServerStage.CreateResponse;
                return false;
            }

            var key = line.Remove(ind).Trim();
            var value = line.Substring(ind + 1).Trim();
            task.Request.HeaderParameter.Add(key, value);

            return true;
        }

        protected virtual async ValueTask<bool> LoadContent(WebProgressTask task, NetworkReader reader, StringBuilder? debugBuilder)
        {
            if (!task.Request.HeaderParameter.TryGetValue("Content-Length", out string strLength))
                return true;
            
            if (!int.TryParse(strLength, out int length) || length < 0)
            {
                WebServerLog.Add(ServerLogType.Error, GetType(), "Header", "Bad Request, invalid content length");
                task.Response.StatusCode = HttpStateCode.BadRequest;
                task.NextStage = ServerStage.CreateResponse;
                return false;
            }

            var buffer = await reader.ReadMemoryAsync(length).ConfigureAwait(false);
            debugBuilder?.Append(Encoding.UTF8.GetChars(buffer.ToArray()));
            debugBuilder?.AppendLine();

            task.Request.Post.SetPost(
                buffer,
                task.Request.HeaderParameter.TryGetValue("Content-Type", out string contentType)
                    ? contentType : null
            );

            return true;
        }

        public override async Task ProgressTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            _ = task.NetworkStream ?? throw new ArgumentNullException(nameof(task.NetworkStream));

            var reader = new NetworkReader(task.NetworkStream);
            StringBuilder? debugBuilder = null;

            try
            {
                debugBuilder = await DebugStartRequest().ConfigureAwait(false);

                // wait until some data is received.
                if (!await WaitForData(task))
                    return;
                
                // read first header line
                var line = await ReadLine(task, reader).ConfigureAwait(false);
                if (line == null)
                    return;
                debugBuilder?.AppendLine(line);
                if (!ParseFirstHeaderLine(task, line))
                    return;
                
                // read all other header lines
                while (!string.IsNullOrWhiteSpace(line = await ReadLine(task, reader)))
                {
                    debugBuilder?.AppendLine(line);
                    if (!ParseOtherHeaderLine(task, line))
                        return;
                }
                debugBuilder?.AppendLine();

                // read content if possible
                if (!await LoadContent(task, reader, debugBuilder))
                    return;
                
                await DebugConnection(task).ConfigureAwait(false);
            }
            finally
            {
                await DebugFinishRequest(debugBuilder).ConfigureAwait(false);
            }
        }
    }
}