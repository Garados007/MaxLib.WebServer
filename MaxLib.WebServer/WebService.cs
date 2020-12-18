using System;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    public abstract class WebService
    {
        public ServerStage Stage { get; private set; }

        public WebService(ServerStage stage)
        {
            Stage = stage;
        }

        public abstract Task ProgressTask(WebProgressTask task);

        public abstract bool CanWorkWith(WebProgressTask task);

        public event EventHandler? PriorityChanged;

        [Obsolete("Use PriorityChanged instead")]
        public event EventHandler? ImportanceChanged;

        WebServicePriority priority = WebServicePriority.Normal;
        public WebServicePriority Priority
        {
            get => priority;
            protected set 
            {
                priority = value;
                ImportanceChanged?.Invoke(this, EventArgs.Empty);
                PriorityChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [Obsolete("Use Priority instead")]
        public WebProgressImportance Importance
        {
            get => (WebProgressImportance)Priority;
            protected set => Priority = (WebServicePriority)value;
        }
    }
}
