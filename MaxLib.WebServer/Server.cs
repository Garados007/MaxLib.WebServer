using MaxLib.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    /// <summary>
    /// A web server that supports the HTTP protocol. This is the bare bone of the server stack.
    /// For functionality you need to add the needed <see cref="WebService" />.
    /// </summary>
    public class Server : IDisposable
    {
        /// <summary>
        /// The added <see cref="WebService" /> divided in their <see cref="ServerStage" />. These
        /// are called in their order for handling user requests.
        /// </summary>
        public FullDictionary<ServerStage, WebServiceGroup> WebServiceGroups { get; }

        /// <summary>
        /// The settings that was used to create this server. Changing these can modify the server
        /// behaveour at runtime.
        /// </summary>
        public WebServerSettings Settings { get; protected set; }

        // Server activity

        protected TcpListener? Listener;
        protected Thread? ServerThread;
        /// <summary>
        /// true if the server is currently running and listening for incomming requests; otherwise
        /// false
        /// </summary>
        public bool ServerExecution { get; protected set; }
        /// <summary>
        /// The connections that are marked with the <c>Keep-Alive</c> header that allows to send
        /// new requests faster.
        /// </summary>
        public SyncedList<HttpConnection> KeepAliveConnections { get; } = new SyncedList<HttpConnection>();
        /// <summary>
        /// All connections that are currently active. Some of these are marked with
        /// <c>Keep-Alive</c> and some will be closed after finishing their requests.<br/>
        /// If the connection changes its protocol (e.g. WebSocket) it becomes removed from this
        /// list.
        /// </summary>
        public SyncedList<HttpConnection> AllConnections { get; } = new SyncedList<HttpConnection>();


        /// <summary>
        /// A web server that supports the HTTP protocol. This is the bare bone of the server stack.
        /// For functionality you need to add the needed <see cref="WebService" />.
        /// </summary>
        /// <param name="settings">The provided settings to create the server with</param>
        public Server(WebServerSettings settings)
        {
            Settings = settings;
            WebServiceGroups = new FullDictionary<ServerStage, WebServiceGroup>((k) => new WebServiceGroup(k));
            WebServiceGroups.FullEnumKeys();
        }

        /// <summary>
        /// Initialize the server with a basic set of web services. These are: <see
        /// cref="Services.HttpRequestParser" />, <see cref="Services.HttpHeaderSpecialAction" />,
        /// <see cref="Services.Http404Service" />, <see cref="Services.HttpResponseCreator" /> and
        /// <see cref="Services.HttpSender" />. <br/> With these you have basic functionality and a
        /// working web server that can deliver 404 answers for every request.
        /// </summary>
        public virtual void InitialDefault()
        {
            //Pre parse request
            AddWebService(new Services.HttpRequestParser());
            //post parse request
            AddWebService(new Services.HttpHeaderSpecialAction());
            //pre create document
            AddWebService(new Services.Http404Service());
            //pre create response
            AddWebService(new Services.HttpResponseCreator());
            //send response
            AddWebService(new Services.HttpSender());
        }

        /// <summary>
        /// Add a new web service to the server and integrate its services. This can be done at
        /// runtime but it is preferred to do this before the <see cref="Start" />.
        /// </summary>
        /// <param name="webService">the web service to add.</param>
        public virtual void AddWebService(WebService webService)
        {
            _ = webService ?? throw new ArgumentNullException(nameof(webService));
            WebServiceGroups[webService.Stage].Add(webService);
        }

        /// <summary>
        /// Returns true if the specified web service already exists.
        /// </summary>
        /// <param name="webService">the web service to check for</param>
        /// <returns>true if found; otherwise false</returns>
        public virtual bool ContainsWebService(WebService webService)
        {
            if (webService == null) 
                return false;
            return WebServiceGroups[webService.Stage].Contains(webService);
        }

        /// <summary>
        /// Remove a web service from the server. This can be done at runtime.
        /// </summary>
        /// <param name="webService">the web service to remove</param>
        public virtual void RemoveWebService(WebService webService)
        {
            if (webService == null) 
                return;
            WebServiceGroups[webService.Stage].Remove(webService);
        }

        /// <summary>
        /// Gets a web service with the specific type. This is usefull to search services later on.
        /// </summary>
        /// <typeparam name="T">the type to search for</typeparam>
        /// <returns>the web service if found; otherwise null</returns>
        public virtual T? GetWebService<T>()
            where T : WebService
            => GetWebServices<T>().FirstOrDefault();

        /// <summary>
        /// Gets all web services with the specific type. This is usefull to search services later
        /// on.
        /// </summary>
        /// <typeparam name="T">the type to search for</typeparam>
        /// <returns>all found web services</returns>
        public virtual IEnumerable<T> GetWebServices<T>()
            where T : WebService
        {
            foreach (var group in WebServiceGroups)
                foreach (var service in group.Value.GetAll<T>())
                    yield return service;
        }

        /// <summary>
        /// Starts the listening and handling of incoming requests.
        /// </summary>
        public virtual void Start()
        {
            WebServerLog.Add(ServerLogType.Information, GetType(), "StartUp", "Start Server on Port {0}", Settings.Port);
            ServerExecution = true;
            Listener = new TcpListener(new IPEndPoint(Settings.IPFilter, Settings.Port));
            Listener.Start();
            ServerThread = new Thread(ServerMainTask)
            {
                Name = "ServerThread - Port: " + Settings.Port.ToString()
            };
            ServerThread.Start();
        }

        /// <summary>
        /// Stops the listening of incoming requests. Currently executing requests will be finished
        /// but not cancelled.
        /// </summary>
        public virtual void Stop()
        {
            WebServerLog.Add(ServerLogType.Information, GetType(), "StartUp", "Stopped Server");
            ServerExecution = false;
            ServerThread?.Join();
        }
        
        protected virtual void ServerMainTask()
        {
            if (Listener == null)
                return;
            WebServerLog.Add(ServerLogType.Information, GetType(), "StartUp", "Server successfully started");
            var watch = new Stopwatch();
            while (ServerExecution)
            {
                watch.Restart();
                //request pending connections
                int step = 0;
                while (step < 10)
                {
                    if (!Listener.Pending()) break;
                    step++;
                    ClientConnected(Listener.AcceptTcpClient());
                }
                //request keep alive connections
                for (int i = 0; i < KeepAliveConnections.Count; ++i)
                {
                    HttpConnection kas;
                    try { kas = KeepAliveConnections[i]; }
                    catch { continue; }
                    if (kas == null) 
                        continue;

                    if ((kas.NetworkClient != null && !kas.NetworkClient.Connected) || 
                        (kas.LastWorkTime != -1 &&
                            kas.LastWorkTime + Settings.ConnectionTimeout < Environment.TickCount
                        )
                    )
                    {
                        kas.NetworkClient?.Close();
                        kas.NetworkStream?.Dispose();
                        AllConnections.Remove(kas);
                        KeepAliveConnections.Remove(kas);
                        --i;
                        continue;
                    }

                    if (kas.NetworkClient != null && kas.NetworkClient.Available > 0 && 
                        kas.LastWorkTime != -1
                    )
                    {
                        _ = Task.Run(() => SafeClientStartListen(kas));
                    }
                }

                //Warten
                if (Listener.Pending()) 
                    continue;
                var delay = Settings.ConnectionDelay;
                if (delay > TimeSpan.Zero)
                {
                    var time = delay - watch.Elapsed;
                    if (time <= TimeSpan.Zero)
                        time = delay;
                    Thread.Sleep(time);
                }
            }
            watch.Stop();
            Listener.Stop();
            for (int i = 0; i < AllConnections.Count; ++i) 
                AllConnections[i].NetworkClient?.Close();
            AllConnections.Clear();
            KeepAliveConnections.Clear();
            WebServerLog.Add(ServerLogType.Information, GetType(), "StartUp", "Server successfully stopped");
        }

        protected virtual void ClientConnected(TcpClient client)
        {
            //prepare session
            var connection = new HttpConnection()
            {
                NetworkClient = client,
                Ip = client.Client.RemoteEndPoint is IPEndPoint iPEndPoint
                    ? iPEndPoint.Address.ToString()
                    : client.Client.RemoteEndPoint?.ToString(),
            };
            AllConnections.Add(connection);
            //listen to connection
            _ = Task.Run(async () => await SafeClientStartListen(connection)).ConfigureAwait(false);
        }

        protected virtual async Task SafeClientStartListen(HttpConnection connection)
        {
            if (Debugger.IsAttached)
                await ClientStartListen(connection).ConfigureAwait(false);
            else
            {
                try { await ClientStartListen(connection).ConfigureAwait(false); }
                catch (Exception e)
                {
                    WebServerLog.Add(
                        ServerLogType.FatalError, 
                        GetType(), 
                        "Unhandled Exception", 
                        $"{e.GetType().FullName}: {e.Message} in {e.StackTrace}");
                }
            }
        }

        protected virtual async Task ClientStartListen(HttpConnection connection)
        {
            connection.LastWorkTime = -1;
            if (connection.NetworkClient != null && connection.NetworkClient.Connected)
            {
                WebServerLog.Add(ServerLogType.Information, GetType(), "Connection", "Listen to Connection {0}", 
                    connection.NetworkClient?.Client.RemoteEndPoint);
                var task = PrepairProgressTask(connection);
                if (task == null)
                {
                    WebServerLog.Add(ServerLogType.Information, GetType(), "Connection",
                        $"Cannot establish data stream to {connection.Ip}");
                    RemoveConnection(connection);
                    return;
                }

                var start = task.Monitor.Enabled ? DateTime.UtcNow : DateTime.MinValue;

                try
                {
                    await ExecuteTaskChain(task).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    task.Monitor.Current.Log("Unhandled exception: {0}", e);
                    WebServerLog.Add(ServerLogType.Error, GetType(), "runtime exception", $"unhandled exception: {e}");
                    throw;
                }
                finally
                {

                    if (Settings.MonitoringOutputDirectory is string monitorOut && task.Monitor.Enabled)
                        await task.Monitor.Save(monitorOut, start, task); 

                }

                if (task.SwitchProtocolHandler != null)
                {
                    KeepAliveConnections.Remove(connection);
                    AllConnections.Remove(connection);
                    task.Dispose();
                    _ = task.SwitchProtocolHandler();
                    return;
                }

                if (task.Request.FieldConnection == HttpConnectionType.KeepAlive)
                {
                    if (!KeepAliveConnections.Contains(connection)) 
                        KeepAliveConnections.Add(connection);
                }
                else RemoveConnection(connection);

                connection.LastWorkTime = Environment.TickCount;
                task.Dispose();
            }
            else RemoveConnection(connection);
        }

        protected void RemoveConnection(HttpConnection connection)
        {
            _ = connection ?? throw new ArgumentNullException(nameof(connection));
            if (KeepAliveConnections.Contains(connection))
                KeepAliveConnections.Remove(connection);
            AllConnections.Remove(connection);
            connection.NetworkClient?.Close();
        }

        internal protected virtual async Task ExecuteTaskChain(WebProgressTask task, ServerStage terminationState = ServerStage.FINAL_STAGE)
        {
            if (task == null) return;
            while (true)
            {
                using var watch = task.Monitor.Watch(this, $"Web Service Group: {task.CurrentStage}");
                await WebServiceGroups[task.CurrentStage].Execute(task).ConfigureAwait(false);
                if (task.CurrentStage == terminationState) 
                    break;
                task.CurrentStage = task.NextStage;
                task.NextStage = task.NextStage == ServerStage.FINAL_STAGE
                    ? ServerStage.FINAL_STAGE
                    : (ServerStage)((int)task.NextStage + 1);
            }
        }

        protected virtual WebProgressTask? PrepairProgressTask(HttpConnection connection)
        {
            var stream = connection.NetworkStream;
            if (stream == null)
                try
                {
                    stream = connection.NetworkStream = connection.NetworkClient?.GetStream();
                }
                catch (InvalidOperationException)
                { return null; }
            var task = new WebProgressTask
            {
                CurrentStage = ServerStage.FIRST_STAGE,
                NextStage = (ServerStage)((int)ServerStage.FIRST_STAGE + 1),
                Server = this,
                Connection = connection,
                NetworkStream = stream,
            };
            if (Settings.MonitoringOutputDirectory != null)
                task.EnableMonitoring();
            return task;
        }

#if NET5_0_OR_GREATER
        /// <summary>
        /// The cancellation token that is used by <see cref="RunAsync(bool, bool, bool)" />. This
        /// property is set after the call to the Run method was initiated.
        /// </summary>
#else
        /// <summary>
        /// The cancellation token that is used by <see cref="RunAsync(bool, bool)" />. This
        /// property is set after the call to the Run method was initiated.
        /// </summary>
#endif
        public CancellationTokenSource? RunToken { get; private set; }

#if NET5_0_OR_GREATER
        /// <summary>
        /// Starts the server and waits for the completion of it. This will also populate <see
        /// cref="RunToken" />.
        /// </summary>
        /// <param name="cancelFromConsoleEvent">
        /// Cancel the execution after a Ctrl+C or Ctrl+Break was received.
        /// </param>
        /// <param name="cancelFromAssemblyUnload">
        /// Cancel the execution if an assembly unload was received.
        /// </param>
        /// /// <param name="cancelFromConsoleInput">
        /// Cancel the execution after the user pressed the letter 'q' in the terminal.
        /// </param>
        /// <returns>the execution task</returns>
#else
        /// <summary>
        /// Starts the server and waits for the completion of it. This will also populate <see
        /// cref="RunToken" />.
        /// </summary>
        /// <param name="cancelFromConsoleEvent">
        /// Cancel the execution after a Ctrl+C or Ctrl+Break was received.
        /// </param>
        /// /// <param name="cancelFromConsoleInput">
        /// Cancel the execution after the user pressed the letter 'q' in the terminal.
        /// </param>
        /// <returns>the execution task</returns>
#endif
        public async Task RunAsync(
            bool cancelFromConsoleEvent = true,
#if NET5_0_OR_GREATER        
            bool cancelFromAssemblyUnload = true,
#endif            
            bool cancelFromConsoleInput = false
        )
        {
            var token = new CancellationTokenSource();
            RunToken = token;

            if (cancelFromConsoleEvent)
                Console.CancelKeyPress += (_, e) =>
                {
                    if (token != RunToken)
                        return;
                    e.Cancel = true;
                    WebServerLog.Add(
                        ServerLogType.Information,
                        GetType(),
                        "cancel",
                        "console cancel received: {0}",
                        e.SpecialKey
                    );
                    if (!token.IsCancellationRequested)
                        token.Cancel();
                };
            
#if NET5_0_OR_GREATER
            if (cancelFromAssemblyUnload)
                System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += _ =>
                {
                    if (token != RunToken)
                        return;
                    WebServerLog.Add(
                        ServerLogType.Information,
                        GetType(),
                        "cancel",
                        "assembly unload received"
                    );
                    if (!token.IsCancellationRequested)
                        token.Cancel();
                };
#endif

            if (cancelFromConsoleInput)
                _ = Task.Run(() =>
                {
                    if (token != RunToken)
                        return;
                    while (Console.Read() != (int)'q');
                    WebServerLog.Add(
                        ServerLogType.Information,
                        GetType(),
                        "cancel",
                        "console key q received"
                    );
                    if (!token.IsCancellationRequested)
                        token.Cancel();
                });
            
            if (!ServerExecution)
                Start();

            try { await Task.Delay(-1, token.Token); }
            catch (TaskCanceledException) {}

            Stop();
        }

        /// <summary>
        /// Stops the server and dispose all services
        /// </summary>
        public void Dispose()
        {
            if (RunToken != null && !RunToken.IsCancellationRequested)
                RunToken.Cancel();
            if (ServerExecution)
                Stop();
            foreach (var group in WebServiceGroups)
                group.Value.Dispose();
        }
    }
}
