using System;
using System.Net.Security;
using System.Security.Cryptography;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Sessions
{
    /// <summary>
    /// Controls the <c>HttpOnly</c>/<c>Secure</c>/<c>SameSite</c> attributes
    /// <see cref="SessionServiceBase" /> issues its session cookie with.
    /// </summary>
    public enum CookieSecurityMode
    {
        /// <summary>
        /// Detect per request whether the connection is encrypted (<see
        /// cref="WebProgressTask.NetworkStream" /> is an <see cref="SslStream" />) and use
        /// <see cref="Strict" /> rules over it, <see cref="Lax" /> rules otherwise. This is
        /// only accurate if this server terminates TLS itself (<c>SecureWebServer</c>/
        /// <c>DualSecureWebServer</c>) - if a reverse proxy in front of it does instead, every
        /// connection this library ever sees is plain HTTP, and <see cref="Strict" /> should be
        /// set explicitly.
        /// </summary>
        Auto,
        /// <summary>
        /// Always issue the session cookie with <c>HttpOnly</c>, <c>Secure</c>, and
        /// <c>SameSite=Strict</c>, regardless of what this server itself observes. Use this if a
        /// reverse proxy (e.g. nginx) terminates TLS in front of it, since <see cref="Auto" />
        /// would otherwise never detect an HTTPS connection.
        /// </summary>
        Strict,
        /// <summary>
        /// Always issue the session cookie with just <c>HttpOnly</c> and
        /// <c>SameSite=Lax</c> - no <c>Secure</c> flag, so it also works over plain HTTP.
        /// </summary>
        Lax,
    }

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

        public string CookiePath { get; set; } = "/";

        public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(30);

        /// <summary>
        /// Controls the security attributes of the issued session cookie. Defaults to
        /// <see cref="CookieSecurityMode.Auto" />, which is only accurate if this server
        /// terminates TLS itself; set <see cref="CookieSecurityMode.Strict" /> if a reverse
        /// proxy does that instead. See the "Sessions" section in <c>README.md</c>.
        /// </summary>
        public CookieSecurityMode CookieSecurity { get; set; } = CookieSecurityMode.Auto;

        public override async Task ProgressTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            var cookie = task.Request?.Cookie.Get("Session");
            var key = cookie?.ValueString;
            if (key != null && await IsKeyAvailable(key).ConfigureAwait(false))
                // a client-supplied id that was never issued by this server must not be
                // adopted, or any attacker could fix a victim's session id in advance
                key = null;
            if (key == null)
            {
                key = await GenerateSessionKey().ConfigureAwait(false);
                SetSessionCookie(task, key);
            }
            task.Session = await Get(key).ConfigureAwait(false);
        }

        /// <summary>
        /// Issues a brand-new session id, migrates the current session's data to it, and
        /// updates the "Session" cookie accordingly. Call this after any change in
        /// privilege (most importantly right after a successful login), so that a session
        /// id an attacker may have set on the client beforehand becomes worthless
        /// afterwards.
        /// </summary>
        /// <param name="task">the current progress task</param>
        /// <returns>the new session, already stored as <c>task.Session</c></returns>
        public async Task<Session> RotateSessionKey(WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            var newKey = await GenerateSessionKey().ConfigureAwait(false);
            var newSession = await Get(newKey).ConfigureAwait(false);
            if (task.Session != null)
                foreach (var pair in task.Session)
                    newSession[pair.Key] = pair.Value;
            task.Session = newSession;
            SetSessionCookie(task, newKey);
            return newSession;
        }

        private void SetSessionCookie(WebProgressTask task, string key)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            var strict = CookieSecurity switch
            {
                CookieSecurityMode.Strict => true,
                CookieSecurityMode.Lax => false,
                _ => task.NetworkStream is SslStream,
            };
            task.Request.Cookie.AddedCookies["Session"] =
                new HttpCookie.Cookie(
                    "Session",
                    key,
                    DateTime.UtcNow + MaxAge,
                    (int)MaxAge.TotalSeconds,
                    CookiePath,
                    httpOnly: true,
                    secure: strict,
                    sameSite: strict ? HttpCookie.SameSiteMode.Strict : HttpCookie.SameSiteMode.Lax
                );
        }

        /// <summary>
        /// This searches the internal session storage for a session. If no session is found
        /// a new session will be created.
        /// </summary>
        /// <param name="key">the key to look for</param>
        /// <returns>the session with its data</returns>
        protected abstract ValueTask<Session> Get(string key);

        /// <summary>
        /// Checks whether a session key is not yet used by any stored session, i.e. whether
        /// it is safe to hand out as a freshly generated id. Also used to reject a
        /// client-supplied id that does not correspond to a session this server ever
        /// issued (see <see cref="ProgressTask(WebProgressTask)"/>).
        /// </summary>
        /// <param name="key">the key to check</param>
        /// <returns>true if no session is currently stored under this key</returns>
        protected abstract ValueTask<bool> IsKeyAvailable(string key);

        protected virtual async ValueTask<string> GenerateSessionKey()
        {
            var key = new byte[16];
            while (true)
            {
                RandomNumberGenerator.Fill(key);
                var stringKey = Convert.ToBase64String(key);
                if (await IsKeyAvailable(stringKey).ConfigureAwait(false))
                    return stringKey;
            }
        }
    }
}