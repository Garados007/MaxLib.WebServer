using MaxLib.Collections;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

//Source: Wikipedia, SelfHTML

namespace MaxLib.WebServer
{
    public class Server
    {
        public FullDictionary<ServerStage, WebServiceGroup> WebServiceGroups { get; }

        public WebServerSettings Settings { get; protected set; }

        //Serveraktivitäten

        protected TcpListener Listener;
        protected Thread ServerThread;
        public bool ServerExecution { get; protected set; }
        public SyncedList<HttpConnection> KeepAliveConnections { get; } = new SyncedList<HttpConnection>();
        public SyncedList<HttpConnection> AllConnections { get; } = new SyncedList<HttpConnection>();

        public Server(WebServerSettings settings)
        {
            Settings = settings;
            WebServiceGroups = new FullDictionary<ServerStage, WebServiceGroup>((k) => new WebServiceGroup(k));
            WebServiceGroups.FullEnumKeys();
        }

        public virtual void InitialDefault()
        {
            //Pre parse request
            AddWebService(new Services.HttpHeaderParser());
            //post parse request
            AddWebService(new Services.HttpHeaderPostParser());
            AddWebService(new Services.HttpDocumentFinder());
            AddWebService(new Services.HttpHeaderSpecialAction());
            //pre create document
            AddWebService(new Services.StandardDocumentLoader());
            AddWebService(new Services.HttpDirectoryMapper(true));
            AddWebService(new Services.Http404Service());
            //pre create response
            AddWebService(new Services.HttpResponseCreator());
            //send response
            AddWebService(new Services.HttpSender());
        }

        public virtual void AddWebService(WebService webService)
        {
            _ = webService ?? throw new ArgumentNullException(nameof(webService));
            WebServiceGroups[webService.Stage].Add(webService);
        }

        public virtual bool ContainsWebService(WebService webService)
        {
            if (webService == null) 
                return false;
            return WebServiceGroups[webService.Stage].Contains(webService);
        }

        public virtual void RemoveWebService(WebService webService)
        {
            if (webService == null) 
                return;
            WebServiceGroups[webService.Stage].Remove(webService);
        }

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

        public virtual void Stop()
        {
            WebServerLog.Add(ServerLogType.Information, GetType(), "StartUp", "Stopped Server");
            ServerExecution = false;
            ServerThread.Join();
        }
        
        protected virtual void ServerMainTask()
        {
            WebServerLog.Add(ServerLogType.Information, GetType(), "StartUp", "Server succesfuly started");
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
                    if (kas == null) continue;

                    if (!kas.NetworkClient.Connected || (kas.LastWorkTime != -1 &&
                        kas.LastWorkTime + Settings.ConnectionTimeout < Environment.TickCount))
                    {
                        kas.NetworkClient.Close();
                        kas.NetworkStream?.Dispose();
                        AllConnections.Remove(kas);
                        KeepAliveConnections.Remove(kas);
                        --i;
                        continue;
                    }

                    if (kas.NetworkClient.Available > 0 && kas.LastWorkTime != -1)
                    {
                        _ = Task.Run(() => SafeClientStartListen(kas));
                    }
                }

                //Warten
                if (Listener.Pending()) 
                    continue;
                var time = watch.ElapsedMilliseconds % 20;
                Thread.Sleep(20 - (int)time);
            }
            watch.Stop();
            Listener.Stop();
            for (int i = 0; i < AllConnections.Count; ++i) 
                AllConnections[i].NetworkClient.Close();
            AllConnections.Clear();
            KeepAliveConnections.Clear();
            WebServerLog.Add(ServerLogType.Information, GetType(), "StartUp", "Server succesfuly stopped");
        }

        protected virtual void ClientConnected(TcpClient client)
        {
            //prepare session
            var connection = CreateRandomConnection();
            connection.NetworkClient = client;
            connection.Ip = client.Client.RemoteEndPoint is IPEndPoint iPEndPoint
                ? iPEndPoint.Address.ToString()
                : client.Client.RemoteEndPoint.ToString();
            AllConnections.Add(connection);
            //listen to connection
            _ = Task.Run(async () => await SafeClientStartListen(connection));
        }

        protected virtual async Task SafeClientStartListen(HttpConnection connection)
        {
            if (Debugger.IsAttached)
                await ClientStartListen(connection);
            else
            {
                try { await ClientStartListen(connection); }
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
            if (connection.NetworkClient.Connected)
            {
                WebServerLog.Add(ServerLogType.Information, GetType(), "Connection", "Listen to Connection {0}", 
                    connection.NetworkClient.Client.RemoteEndPoint);
                var task = PrepairProgressTask(connection);
                if (task == null)
                {
                    WebServerLog.Add(ServerLogType.Information, GetType(), "Connection",
                        $"Cannot establish data stream to {connection.Ip}");
                    RemoveConnection(connection);
                    return;
                }

                await ExecuteTaskChain(task);

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
            connection.NetworkClient.Close();
        }

        internal protected virtual async Task ExecuteTaskChain(WebProgressTask task, ServerStage terminationState = ServerStage.FINAL_STAGE)
        {
            if (task == null) return;
            while (true)
            {
                await WebServiceGroups[task.CurrentStage].Execute(task);
                if (task.CurrentStage == terminationState) 
                    break;
                task.CurrentStage = task.CurrentStage;
                task.NextStage = task.NextStage == ServerStage.FINAL_STAGE
                    ? ServerStage.FINAL_STAGE
                    : (ServerStage)((int)task.NextStage + 1);
            }
        }

        protected virtual WebProgressTask PrepairProgressTask(HttpConnection connection)
        {
            var stream = connection.NetworkStream;
            if (stream == null)
                try
                {
                    stream = connection.NetworkStream = connection.NetworkClient.GetStream();
                }
                catch (InvalidOperationException)
                { return null; }
            return new WebProgressTask
            {
                CurrentStage = ServerStage.FIRST_STAGE,
                NextStage = (ServerStage)((int)ServerStage.FIRST_STAGE + 1),
                Document = new HttpDocument(),
                Server = this,
                Connection = connection,
                NetworkStream = stream,
            };
        }

        protected virtual HttpConnection CreateRandomConnection()
        {
            var s = new HttpConnection();
            var r = new Random();
            do
            {
                s.ConnectionKey = new byte[16];
                r.NextBytes(s.ConnectionKey);
            }
            while (AllConnections.Exists((ht) => ht != null && WebServerUtils.BytesEqual(ht.ConnectionKey, s.ConnectionKey)));
            s.LastWorkTime = -1;
            return s;
        }
    }
}
