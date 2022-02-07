using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Testing
{
    public class TestWebServer : Server
    {
        static WebServerSettings GetSettings()
        {
            return new WebServerSettings(80, 0)
            {
                Debug_LogConnections = false,
                Debug_WriteRequests = false,
                IPFilter = IPAddress.Any,
            };
        }

        public TestWebServer()
            : base(GetSettings())
        {

        }

        public override void Start()
        {
            ServerExecution = true;
        }

        public override void Stop()
        {
            ServerExecution = false;
        }

        protected override void ServerMainTask()
        {
        }

        protected override void ClientConnected(TcpClient client)
        {
        }

        protected override Task SafeClientStartListen(HttpConnection connection)
            => Task.CompletedTask;

        protected override Task ClientStartListen(HttpConnection connection)
            => Task.CompletedTask;

        public Task Execute(WebProgressTask task, ServerStage terminationState = ServerStage.FINAL_STAGE)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            return ExecuteTaskChain(task, terminationState);
        }

        public new void RemoveConnection(HttpConnection connection)
            => base.RemoveConnection(connection);

        public TestTask CreateTest()
            => new TestTask(this);
    }
}
