using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Builder.Runtime
{
    public class ServiceGroup : WebServiceCollection
    {
        public List<Tools.RuleAttributeBase> Rules { get; }

        /// <summary>
        /// The type this group was generated from, if it was built by
        /// <see cref="Tools.Generator.GenerateClass(Type)" />. Null for a hand-assembled group.
        /// </summary>
        public Type? SourceType { get; set; }

        public ServiceGroup(List<Tools.RuleAttributeBase> rules)
        {
            Rules = rules;
        }

        public override bool CheckPrecondition(WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            if (!base.CheckPrecondition(task))
                return false;

            Dictionary<string, object?> vars;
            if (task.Document.Information.TryGetValue(MethodService.InfoKey, out object? infoKeyObj) &&
                infoKeyObj is Dictionary<string, object?> info
            )
                vars = info;
            else vars = new Dictionary<string, object?>();

            foreach (var rule in Rules)
            {
                task.Monitor.Current.Log("check rule {0}", rule);
                if (!rule.CanWorkWith(task, vars))
                    return false;
            }

            task.Document[MethodService.InfoKey] = vars;
            task.Monitor.Current.Log("rule success");
            return true;
        }
    }
}