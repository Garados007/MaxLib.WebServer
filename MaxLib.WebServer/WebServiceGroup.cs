﻿using MaxLib.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

#nullable enable

namespace MaxLib.WebServer
{
    public class WebServiceGroup : IDisposable
    {
        public ServerStage Stage { get; private set; }

        public WebServiceGroup(ServerStage stage)
        {
            Stage = stage;
            Services = new PriorityList<WebServicePriority, WebService>();
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

        protected PriorityList<WebServicePriority, WebService> Services { get; private set; }

        public void Add(WebService service)
        {
            _ = service ?? throw new ArgumentNullException(nameof(service));
            service.PriorityChanged += Service_PriorityChanged;
            Services.Add(service.Priority, service);
        }

        private void Service_PriorityChanged(object? sender, EventArgs e)
        {
            var service = (WebService)sender!;
            Services.ChangePriority(service.Priority, service);
        }

        public bool Remove(WebService service)
        {
            if (Services.Remove(service))
            {
                service.PriorityChanged -= Service_PriorityChanged;
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
            return (T)Services.Find((ws) => ws is T);
        }

        public IEnumerable<T> GetAll<T>() where T : WebService
        {
            return Services.Where(x => x is T).Cast<T>();
        }

        public virtual async Task Execute(WebProgressTask task)
        {
            var se = SingleExecution;
            var set = false;
            var services = Services.ToArray();
            foreach (var service in services)
            {
                if (task.Connection?.NetworkClient != null && !task.Connection.NetworkClient.Connected) return;
                if (service is WebService2 service2)
                {
                    var watch = task.Monitor.Watch(service, "CanWorkWith()");
                    if (service2.CanWorkWith(task, out object? data))
                    {
                        watch.Dispose();
                        if (task.Connection?.NetworkClient != null && !task.Connection.NetworkClient.Connected) return;
                        try
                        {
                            watch = task.Monitor.Watch(service, "ProgressTask()");
                            await service2.ProgressTask(task, data).ConfigureAwait(false);
                        }
                        catch (HttpException e)
                        {
                            task.Response.StatusCode = e.StateCode;
                            if (e.DataSource != null)
                                task.Document.DataSources.Add(e.DataSource);
                        }
                        finally
                        {
                            watch.Dispose();
                        }
                        task.Document[Stage] = true;
                        if (se) 
                            return;
                        set = true;
                    }
                    else watch.Dispose();
                }
                else 
                {
                    var watch = task.Monitor.Watch(service, "CanWorkWith()");
                    if (service.CanWorkWith(task))
                    {
                        watch.Dispose();
                        if (task.Connection?.NetworkClient != null && !task.Connection.NetworkClient.Connected) return;
                        try
                        {
                            watch = task.Monitor.Watch(service, "ProgressTask()");
                            await service.ProgressTask(task).ConfigureAwait(false);
                        }
                        catch (HttpException e)
                        {
                            task.Response.StatusCode = e.StateCode;
                            if (e.DataSource != null)
                                task.Document.DataSources.Add(e.DataSource);
                        }
                        finally
                        {
                            watch.Dispose();
                        }
                        task.Document[Stage] = true;
                        if (se) 
                            return;
                        set = true;
                    }
                    else watch.Dispose();
                }
            }
            if (!set) 
                task.Document[Stage] = false;
        }

        public void Dispose()
        {
            foreach (var service in Services)
                service.Dispose();
        }
    }
}
