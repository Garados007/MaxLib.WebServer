using System;
using System.Threading.Tasks;

namespace MaxLib.WebServer
{
    public abstract class WebService
    {
        public ServerStage Stage { get; private set; }

        public WebService(ServerStage stage)
        {
            Stage = stage;
            Importance = WebProgressImportance.Normal;
        }

        public abstract Task ProgressTask(WebProgressTask task);

        public abstract bool CanWorkWith(WebProgressTask task);

        public event EventHandler ImportanceChanged;

        WebProgressImportance importance;
        public WebProgressImportance Importance
        {
            get => importance;
            protected set
            {
                importance = value;
                ImportanceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
