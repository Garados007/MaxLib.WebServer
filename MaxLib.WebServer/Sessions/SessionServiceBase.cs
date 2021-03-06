using System;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Sessions
{
    public abstract class SessionServiceBase : WebService
    {
        public SessionServiceBase() 
            : base(ServerStage.ParseRequest)
        {
            // HttpHeaderPostParser has a default priority of VeryHigh. This needs to be executed 
            // right after it but before others.
            Priority = (WebServicePriority)(
                ((int)WebServicePriority.VeryHigh + (int)WebServicePriority.High) / 2
            );
        }

        public override bool CanWorkWith(WebProgressTask task)
            => true;

        Random random = new Random();

        public string CookiePath { get; set; } = "/";

        public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(30);

        public override async Task ProgressTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            var cookie = task.Request?.Cookie.Get("Session");
            string key;
            if (cookie == null)
            {
                key = await GenerateSessionKey().ConfigureAwait(false);
                task.Request?.Cookie.AddedCookies.Add("Session",
                    new HttpCookie.Cookie(
                        "Session",
                        key,
                        DateTime.UtcNow + MaxAge,
                        (int)MaxAge.TotalSeconds,
                        CookiePath
                    ));
            }
            else
            {
                key = cookie.Value.ValueString;
            }
            task.Session = await Get(key).ConfigureAwait(false);
        }

        /// <summary>
        /// This searches the internal session storage for a session. If no session is found
        /// a new session will be created.
        /// </summary>
        /// <param name="key">the key to look for</param>
        /// <returns>the session with its data</returns>
        protected abstract ValueTask<Session> Get(string key);

        protected abstract ValueTask<bool> IsKeyAvailable(string key);

        protected virtual async ValueTask<string> GenerateSessionKey()
        {
            var key = new byte[16];
            while (true)
            {
                random.NextBytes(key);
                var stringKey = Convert.ToBase64String(key);
                if (await IsKeyAvailable(stringKey))
                    return stringKey;
            }
        }
    }
}