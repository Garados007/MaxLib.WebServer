using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Builder.Runtime
{
    public class MethodService : WebService2<object?[]>
    {
        public List<Tools.RuleAttributeBase> Rules { get; }

        public List<IParameter> Parameters { get; }

        public MethodInfo Method { get; }

        public Func<WebProgressTask, object?, Task> Result { get; }

        public object MethodClass { get; }

        public MethodService(List<Tools.RuleAttributeBase> rules, List<IParameter> parameters,
            MethodInfo method, Func<WebProgressTask, object?, Task> result,
            WebServicePriority priority
        )
            : base(ServerStage.CreateDocument)
        {
            Rules = rules;
            Parameters = parameters;
            Method = method;
            Result = result;
            MethodClass = Activator.CreateInstance(method.ReflectedType!)!;
            Priority = priority;
        }

        public override Task ProgressTask(WebProgressTask task, object?[]? data)
        {
            if (data is null)
                return Task.CompletedTask;
            using var watch = task.Monitor.Watch(MethodClass, $"Execute {Method.Name}()");
            var result = Method.Invoke(MethodClass, data);
            GC.KeepAlive(watch);
            return Result(task, result);
        }

        internal static string InfoKey = $"{typeof(MethodService).FullName}-InfoKey";

        public override bool CanWorkWith(WebProgressTask task, out object?[]? data)
        {
            using var watch = task.Monitor.Watch(MethodClass, $"Check {Method.Name}()");
            data = new object[Parameters.Count];
            Dictionary<string, object?> vars;
            if (task.Document.Information.TryGetValue(InfoKey, out object? infoKeyObj) &&
                infoKeyObj is Dictionary<string, object?> info
            )
                vars = info;
            else vars = new Dictionary<string, object?>();
            // verify rules
            foreach (var rule in Rules)
            {
                watch.Log("verify rule {0}", rule);
                if (!rule.CanWorkWith(task, vars))
                    return false;
            }
            // execute parameter
            for (int i = 0; i < Parameters.Count; ++i)
            {
                watch.Log("execute parameter {0}", Parameters[i]);
                var res = Parameters[i].GetValue(task, vars);
                if (!res.HasValue)
                    return false;
                data[i] = res.Value;
            }
            // method is ready to call
            GC.KeepAlive(watch);
            return true;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (MethodClass is IDisposable disposable)
                disposable.Dispose();
        }
    }
}