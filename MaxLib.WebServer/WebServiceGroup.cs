using MaxLib.Collections;
using System;
using System.Threading.Tasks;

namespace MaxLib.WebServer
{
    public class WebServiceGroup
    {
        public ServerStage Stage { get; private set; }

        public WebServiceGroup(ServerStage stage)
        {
            Stage = stage;
            Services = new PriorityList<WebProgressImportance, WebService>();
        }

        public virtual bool SingleExecution
        {
            get
            {
                return Stage switch 
                {
                    ServerStage.ReadRequest => true,
                    ServerStage.ParseRequest => false,
                    ServerStage.CreateDocument => true,
                    ServerStage.ProcessDocument => false,
                    ServerStage.CreateResponse => false,
                    ServerStage.SendResponse => true,
                    ServerStage.Cleanup => false,
                    _ => throw new NotImplementedException(
                        $"Stage {Stage} is not implemented"
                    ),
                };
            }
        }

        protected PriorityList<WebProgressImportance, WebService> Services { get; private set; }

        public void Add(WebService service)
        {
            _ = service ?? throw new ArgumentNullException(nameof(service));
            service.ImportanceChanged += Service_ImportanceChanged;
            Services.Add(service.Importance, service);
        }

        private void Service_ImportanceChanged(object sender, EventArgs e)
        {
            var service = sender as WebService;
            Services.ChangePriority(service.Importance, service);
        }

        public bool Remove(WebService service)
        {
            if (Services.Remove(service))
            {
                service.ImportanceChanged -= Service_ImportanceChanged;
                return true;
            }
            else return false;
        }

        public void Clear()
        {
            Services.Clear();
        }

        public bool Contains(WebService service)
        {
            return Services.Contains(service);
        }

        public T Get<T>() where T : WebService
        {
            return Services.Find((ws) => ws is T) as T;
        }

        public virtual async Task Execute(WebProgressTask task)
        {
            var se = SingleExecution;
            var set = false;
            var services = Services.ToArray();
            foreach (var service in services)
            {
                if (task.Connection.NetworkClient != null && !task.Connection.NetworkClient.Connected) return;
                if (service.CanWorkWith(task))
                {
                    if (task.Connection.NetworkClient != null && !task.Connection.NetworkClient.Connected) return;
                    await service.ProgressTask(task);
                    task.Document[Stage] = true;
                    if (se) 
                        return;
                    set = true;
                }
            }
            if (!set) 
                task.Document[Stage] = false;
        }
    }
}
