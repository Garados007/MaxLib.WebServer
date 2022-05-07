using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    public abstract class WebService2 : WebService
    {
        public WebService2(ServerStage stage)
            : base(stage)
        {
        }

        public abstract Task ProgressTask(WebProgressTask task, object? data);

        public abstract bool CanWorkWith(WebProgressTask task, out object? data);

        public override Task ProgressTask(WebProgressTask task)
        {
            if (!task.Document.Information.TryGetValue(this, out object? data))
                data = null;
            return ProgressTask(task, data);
        }

        public override bool CanWorkWith(WebProgressTask task)
        {
            if (!CanWorkWith(task, out object? data))
                return false;
            task.Document[this] = data;
            return true;
        }
    }

    public abstract class WebService2<T> : WebService2
        where T : class
    {
        public WebService2(ServerStage stage)
            : base(stage)
        {
        }

        public abstract Task ProgressTask(WebProgressTask task, T? data);

        public abstract bool CanWorkWith(WebProgressTask task, out T? data);

        public override Task ProgressTask(WebProgressTask task, object? data)
        {
            return ProgressTask(task, data as T);
        }

        public override bool CanWorkWith(WebProgressTask task, out object? data)
        {
            var success = CanWorkWith(task, out T? d);
            data = d;
            return success;
        }
    }
}
