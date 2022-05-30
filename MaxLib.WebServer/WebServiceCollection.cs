using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MaxLib.Collections;

namespace MaxLib.WebServer
{
    /// <summary>
    /// This is a service collection that contains multiple <see cref="WebService" />. For execution
    /// this will search for the first containing service that matches the condition and execute
    /// them. Therefore this service collection is not suitable if you want to use multiple services
    /// in the same stage.
    /// </summary>
    public class WebServiceCollection : WebService2<WebServiceCollection.CallInfo>,
        ICollection<WebService>
    {
        /// <summary>
        /// This is a service collection that contains multiple <see cref="WebService" />. For
        /// execution this will search for the first containing service that matches the condition
        /// and execute them. Therefore this service collection is not suitable if you want to use
        /// multiple services in the same stage.
        /// </summary>
        /// <param name="stage">The <see cref="ServerStage" /> this collection is pinned to</param>
        public WebServiceCollection(ServerStage stage)
            : base(stage)
        {
        }

        /// <summary>
        /// This is a service collection that contains multiple <see cref="WebService" />. For
        /// execution this will search for the first containing service that matches the condition
        /// and execute them. Therefore this service collection is not suitable if you want to use
        /// multiple services in the same stage.<br/>
        /// This constructor will generate a <see cref="WebServiceCollection" /> that is pinned to
        /// <see cref="ServerStage.CreateDocument" />.
        /// </summary>
        public WebServiceCollection()
            : base(ServerStage.CreateDocument)
        {
        }

        public class CallInfo
        {
            public WebService Service { get; }

            public object? Data { get; }

            public CallInfo(WebService service, object? data)
            {
                Service = service;
                Data = data;
            }

            public Task ProgressTask(WebProgressTask task)
            {
                if (Service is WebService2 ws2)
                {
                    return ws2.ProgressTask(task, Data);
                }
                else
                {
                    return Service.ProgressTask(task);
                }
            }
        }

        private PriorityList<WebServicePriority, WebService>  Services { get; }
            = new PriorityList<WebServicePriority, WebService>();

        public override void Dispose()
        {
            base.Dispose();
            foreach (var service in Services)
                service.Dispose();
        }

        public int Count => Services.Count;

        bool ICollection<WebService>.IsReadOnly => false;

        public override Task ProgressTask(WebProgressTask task, CallInfo? data)
        {
            if (data is null)
                return Task.CompletedTask;
            using (task.Monitor.Watch(data.Service, "ProgressTask()"))
                return data.ProgressTask(task);
        }

        /// <summary>
        /// This is precondition that will be checked before all contained <see cref="WebService" 
        /// />s will be checked. This member is to be intended to be overwritten.
        /// </summary>
        /// <param name="task">The task that is currently executed.</param>
        /// <returns>The check result.</returns>
        public virtual bool CheckPrecondition(WebProgressTask task)
        {
            return true;
        }

        public override bool CanWorkWith(WebProgressTask task, out CallInfo? data)
        {
            if (!CheckPrecondition(task))
            {
                data = null;
                return false;
            }
            foreach (var service in Services)
            {
                using var watch = task.Monitor.Watch(service, "CanWorkWith()");
                if (service is WebService2 service2)
                {
                    if (service2.CanWorkWith(task, out object? data_))
                    {
                        data = new CallInfo(service, data_);
                        return true;
                    }
                }
                else
                {
                    if (service.CanWorkWith(task))
                    {
                        data = new CallInfo(service, null);
                        return true;
                    }
                }
                GC.KeepAlive(watch);
            }
            data = null;
            return false;
        }

        public void Add(WebService item)
        {
            if (item.Stage != Stage)
                throw new ArgumentException("invalid stage", nameof(item));
            item.PriorityChanged += Service_PriorityChanged;
            Services.Add(item.Priority, item);
        }

        private void Service_PriorityChanged(object? sender, EventArgs e)
        {
            var service = (WebService)sender!;
            Services.ChangePriority(service.Priority, service);
        }

        public void Clear()
        {
            foreach (var service in Services)
                service.PriorityChanged -= Service_PriorityChanged;
            Services.Clear();
        }

        public bool Contains(WebService item)
        {
            return Services.Contains(item);
        }

        public void CopyTo(WebService[] array, int arrayIndex)
        {
            Services.CopyTo(array, arrayIndex);
        }

        public bool Remove(WebService item)
        {
            if (Services.Remove(item))
            {
                item.PriorityChanged -= Service_PriorityChanged;
                return true;
            }
            else return false;
        }

        public IEnumerator<WebService> GetEnumerator()
        {
            return Services.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}