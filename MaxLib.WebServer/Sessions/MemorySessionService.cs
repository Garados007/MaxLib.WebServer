using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Sessions
{
    public class MemorySessionService : SessionServiceBase
    {
        public Dictionary<string, Session> Sessions { get; }
            = new Dictionary<string, Session>();

        protected override ValueTask<Session> Get(string key)
        {
            _ = key ?? throw new ArgumentNullException(nameof(key));
            if (!Sessions.TryGetValue(key, out Session value))
                Sessions.Add(key, value = new Session());
            value.LastUsed = DateTime.UtcNow;
            return new ValueTask<Session>(value);
        }

        protected override ValueTask<bool> IsKeyAvailable(string key)
            => new ValueTask<bool>(!Sessions.ContainsKey(key));
    }
}