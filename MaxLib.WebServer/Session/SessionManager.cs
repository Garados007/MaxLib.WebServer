using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Session
{
    public static class SessionManager
    {
        static readonly List<SessionInformation> Sessions = new List<SessionInformation>();

        public static void Register(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            var cookie = task.Document.RequestHeader.Cookie.Get("Session");
            if (cookie == null)
            {
                var si = RegisterNewSession(task.Connection);
                task.Connection.ConnectionKey = si.Key;
                task.Connection.AlwaysSyncSessionInformation(si.Information);
                task.Document.RequestHeader.Cookie.AddedCookies.Add("Session", 
                    new HttpCookie.Cookie("Session", Convert.ToBase64String(si.Key), 3600));
            }
            else
            {
                if (!RegisterSession(task.Connection, Convert.FromBase64String(cookie.Value.ValueString)))
                    task.Document.RequestHeader.Cookie.AddedCookies.Add("Session",
                        new HttpCookie.Cookie("Session", Convert.ToBase64String(task.Connection.ConnectionKey), 3600));
            }
        }

        public static bool RegisterSession(HttpConnection connection, byte[] binkey)
        {
            _ = connection ?? throw new ArgumentNullException(nameof(connection));
            _ = binkey ?? throw new ArgumentNullException(nameof(binkey));
            var si = Get(binkey);
            var added = si != null;
            si = si ?? RegisterNewSession(connection);
            connection.ConnectionKey = si.Key;
            connection.AlwaysSyncSessionInformation(si.Information);
            return !added;
        }

        public static SessionInformation RegisterNewSession(HttpConnection connection)
        {
            _ = connection ?? throw new ArgumentNullException(nameof(connection));
            var key = GenerateSessionKey();
            connection.ConnectionKey = key;
            var si = new SessionInformation(key, DateTime.Now);
            Sessions.Add(si);
            return si;
        }

        public static SessionInformation Get(byte[] key)
        {
            _ = key ?? throw new ArgumentNullException(nameof(key));
            return Sessions.Find((si) => WebServerUtils.BytesEqual(si.Key, key));
        }

        public static void DeleteSession(byte[] key)
        {
            _ = key ?? throw new ArgumentNullException(nameof(key));
            var ind = Sessions.FindIndex((si) => si.Key == key);
            if (ind != -1) Sessions.RemoveAt(ind);
        }

        static byte[] GenerateSessionKey()
        {
            var r = new Random();
            var key = new byte[16];
            while (true)
            {
                r.NextBytes(key);
                if (!Sessions.Exists((si) => WebServerUtils.BytesEqual(key, si.Key)))
                    return key;
            }
        }
    }
}
