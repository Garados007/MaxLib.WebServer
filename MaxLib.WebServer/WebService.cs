using System;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    public abstract class WebService : IDisposable
    {
        public ServerStage Stage { get; private set; }

        public WebService(ServerStage stage)
        {
            Stage = stage;
        }

        public abstract Task ProgressTask(WebProgressTask task);

        public abstract bool CanWorkWith(WebProgressTask task);

        public virtual void Dispose()
        {
        }

        public event EventHandler? PriorityChanged;

        WebServicePriority priority = WebServicePriority.Normal;
        public WebServicePriority Priority
        {
            get => priority;
            protected set 
            {
                priority = value;
                PriorityChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
