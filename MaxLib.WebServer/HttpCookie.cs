using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpCookie
    {
        /// <summary>
        /// The <c>SameSite</c> attribute of a <see cref="Cookie" />, controlling whether it is
        /// sent along with cross-site requests.
        /// </summary>
        public enum SameSiteMode
        {
            /// <summary>
            /// No <c>SameSite</c> attribute is emitted at all. Browsers then fall back to their
            /// own default (currently <see cref="Lax" /> in every major browser).
            /// </summary>
            Unspecified,
            /// <summary>
            /// The cookie is withheld on cross-site requests entirely, including top-level
            /// navigation (e.g. following a link from another site). The strongest CSRF
            /// protection, at the cost of the cookie not being sent on the first request a user
            /// makes after such a navigation.
            /// </summary>
            Strict,
            /// <summary>
            /// The cookie is withheld on cross-site requests except top-level navigation (e.g.
            /// following a link from another site still sends it). The common default balance
            /// between CSRF protection and not breaking ordinary cross-site links.
            /// </summary>
            Lax,
            /// <summary>
            /// The cookie is sent on every request, including cross-site ones. Requires
            /// <see cref="Cookie.Secure" /> to be set, or browsers reject the cookie outright.
            /// </summary>
            None,
        }

        [Serializable]
        public readonly struct Cookie
        {
            public ReadOnlyMemory<char> Name { get; }

            public string NameString => Name.ToString();
            public ReadOnlyMemory<char> Value { get; }

            public string ValueString => Value.ToString();
            public DateTime Expires { get; }
            public int MaxAge { get; }
            /// <summary>
            /// Must not contain ';' or control characters; the value is written into the Set-Cookie line without escaping.
            /// </summary>
            public ReadOnlyMemory<char> Path { get; }

            /// <summary>
            /// If <c>true</c>, this cookie is inaccessible to JavaScript (<c>document.cookie</c>)
            /// and is only ever sent as part of an HTTP request - the primary defense against
            /// cookie theft via XSS.
            /// </summary>
            public bool HttpOnly { get; }

            /// <summary>
            /// If <c>true</c>, this cookie is only ever sent over an encrypted (HTTPS)
            /// connection.
            /// </summary>
            public bool Secure { get; }

            /// <summary>
            /// Controls whether this cookie is sent along with cross-site requests. See
            /// <see cref="SameSiteMode" />.
            /// </summary>
            public SameSiteMode SameSite { get; }

            public Cookie(string name, string value)
                : this(name, value, new DateTime(9999, 12, 31), -1, "")
            { }

            public Cookie(string name, string value, DateTime expires)
                : this(name, value, expires, -1, "")
            { }

            public Cookie(string name, string value, int maxAge)
                : this(name, value, new DateTime(9999, 12, 31), maxAge, "")
            { }

            public Cookie(string name, string value, string path)
                : this(name, value, new DateTime(9999, 12, 31), -1, path)
            { }

            public Cookie(string name, string value, DateTime expires, int maxAge, string path)
                : this(name, value, expires, maxAge, path, false, false, SameSiteMode.Unspecified)
            { }

            /// <summary>
            /// Creates a cookie with explicit <see cref="HttpOnly" />/<see cref="Secure" />/
            /// <see cref="SameSite" /> attributes. See <see cref="Sessions.SessionServiceBase" />
            /// for how the session cookie derives sensible defaults for these automatically.
            /// </summary>
            public Cookie(
                string name,
                string value,
                DateTime expires,
                int maxAge,
                string path,
                bool httpOnly,
                bool secure,
                SameSiteMode sameSite)
            {
                Name = name?.AsMemory() ?? throw new ArgumentNullException(nameof(name));
                Value = value?.AsMemory() ?? throw new ArgumentNullException(nameof(value));
                Expires = expires;
                MaxAge = maxAge;
                Path = path?.AsMemory() ?? throw new ArgumentNullException(nameof(path));
                HttpOnly = httpOnly;
                Secure = secure;
                SameSite = sameSite;
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append(WebServerUtils.EncodeUri(Name.ToString()));
                sb.Append('=');
                sb.Append(WebServerUtils.EncodeUri(Value.ToString()));
                if (Expires != new DateTime(9999, 12, 31))
                {
                    sb.Append(";expires=");
                    sb.Append(WebServerUtils.GetDateString(Expires));
                }
                if (MaxAge != -1)
                {
                    sb.Append(";Max-Age=");
                    sb.Append(MaxAge);
                }
                sb.Append(";Path=");
                // Path legitimately contains "/" (unlike Name/Value, it is not fully
                // URI-encoded), so only strip the one thing that can never be legitimate in a
                // cookie attribute - a raw CRLF - rather than escaping the whole value.
                sb.Append(WebServerUtils.RemoveCrLf(Path.ToString()));
                if (HttpOnly)
                    sb.Append(";HttpOnly");
                if (Secure)
                    sb.Append(";Secure");
                if (SameSite != SameSiteMode.Unspecified)
                {
                    sb.Append(";SameSite=");
                    sb.Append(SameSite.ToString());
                }
                return sb.ToString();
            }
        }

        public string CompleteRequestCookie { get; private set; } = "";

        public Dictionary<string, Cookie> AddedCookies { get; }

        public ReadOnlyDictionary<string, Cookie> RequestedCookies { get; private set; }

        public HttpCookie(string cookie)
        {
            ArgumentNullException.ThrowIfNull(cookie);
            AddedCookies = new Dictionary<string, Cookie>();
            RequestedCookies = new ReadOnlyDictionary<string, Cookie>(new Dictionary<string, Cookie>());
            SetRequestCookieString(cookie);
        }

        public Cookie? Get(string name)
        {
            if (AddedCookies.TryGetValue(name, out Cookie cookie))
                return cookie;
            if (RequestedCookies.TryGetValue(name, out Cookie rcookie))
                return rcookie;
            return null;
        }

        public virtual void SetRequestCookieString(string cookie)
        {
            CompleteRequestCookie = cookie ?? throw new ArgumentNullException(nameof(cookie));
            AddedCookies.Clear();
            var reqCookie = new Dictionary<string, Cookie>();
            if (CompleteRequestCookie.Length != 0)
            {
                var tiles = CompleteRequestCookie.Split('&', ';');
                foreach (var tile in tiles)
                {
                    var ind = tile.IndexOf('=', StringComparison.Ordinal);
                    if (ind == -1)
                    {
                        var key = WebServerUtils.DecodeUri(tile.Trim());
                        if (!reqCookie.ContainsKey(key))
                            reqCookie.Add(key, new Cookie(key, ""));
                    }
                    else
                    {
                        var key = WebServerUtils.DecodeUri(tile[..ind].Trim());
                        var value = ind + 1 == tile.Length ? "" : WebServerUtils.DecodeUri(tile[(ind + 1)..]);
                        if (!reqCookie.ContainsKey(key))
                            reqCookie.Add(key, new Cookie(key, value));
                    }
                }
            }
            RequestedCookies = new ReadOnlyDictionary<string, Cookie>(reqCookie);
        }

        public override string ToString()
            => CompleteRequestCookie;
    }
}
