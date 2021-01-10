using System.Collections.ObjectModel;
using System;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpRequestHeader : HttpHeader
    {
        protected override void ResetCache()
        {
            base.ResetCache();
            fieldAccept = null;
            fieldAcceptCharset = null;
            fieldAcceptEncoding = null;
            fieldConnection = null;
            host = null;
            cookie = null;
        }

        public string Url
        {
            get => Location.Url;
            set => Location.SetLocation(value ?? "/");
        }

        public HttpLocation Location { get; } = new HttpLocation("/");

        private Lazy<string>? host = null;
        public string Host
        {
            get
            {
                if (host != null)
                    return host.Value;
                return (host = new Lazy<string>(
                    () => HeaderParameter.TryGetValue("Host", out string value) ?
                        value : ""
                )).Value;
            }
            set
            {
                _ = host ?? throw new ArgumentNullException(nameof(value));
                SetResetLock(true);
                HeaderParameter["Host"] = value;
                host = new Lazy<string>(value);
                SetResetLock(false);
            }
        }

        public HttpPost Post { get; } = new HttpPost();

        private Lazy<ReadOnlyCollection<string>>? fieldAccept = null;
        public ReadOnlyCollection<string> FieldAccept
        {
            get
            {
                if (fieldAccept != null)
                    return fieldAccept.Value;
                return (fieldAccept = new Lazy<ReadOnlyCollection<string>>(
                    () => new ReadOnlyCollection<string>(
                        HeaderParameter.TryGetValue("Accept", out string value) ?
                            value.Split(
                                new[] { ',', ' ' },
                                StringSplitOptions.RemoveEmptyEntries
                            ) : new string[0]
                    )
                )).Value;
            }
            set
            {
                _ = value ?? throw new ArgumentNullException(nameof(value));
                var header = string.Join(", ", value);
                SetResetLock(true);
                HeaderParameter["Accept"] = header;
                fieldAccept = new Lazy<ReadOnlyCollection<string>>(value);
                SetResetLock(false);
            }
        }
        
        private Lazy<ReadOnlyCollection<string>>? fieldAcceptCharset = null;
        public ReadOnlyCollection<string> FieldAcceptCharset
        {
            get
            {
                if (fieldAcceptCharset != null)
                    return fieldAcceptCharset.Value;
                return (fieldAcceptCharset = new Lazy<ReadOnlyCollection<string>>(
                    () => new ReadOnlyCollection<string>(
                        HeaderParameter.TryGetValue("Accept-Charset", out string value) ?
                            value.Split(
                                new[] { ',', ' ' },
                                StringSplitOptions.RemoveEmptyEntries
                            ) : new string[0]
                    )
                )).Value;
            }
            set
            {
                _ = value ?? throw new ArgumentNullException(nameof(value));
                var header = string.Join(", ", value);
                SetResetLock(true);
                HeaderParameter["Accept-Charset"] = header;
                fieldAcceptCharset = new Lazy<ReadOnlyCollection<string>>(value);
                SetResetLock(false);
            }
        }

        private Lazy<ReadOnlyCollection<string>>? fieldAcceptEncoding = null;
        public ReadOnlyCollection<string> FieldAcceptEncoding
        {
            get
            {
                if (fieldAcceptEncoding != null)
                    return fieldAcceptEncoding.Value;
                return (fieldAcceptEncoding = new Lazy<ReadOnlyCollection<string>>(
                    () => new ReadOnlyCollection<string>(
                        HeaderParameter.TryGetValue("Accept-Encoding", out string value) ?
                            value.Split(
                                new[] { ',', ' ' },
                                StringSplitOptions.RemoveEmptyEntries
                            ) : new string[0]
                    )
                )).Value;
            }
            set
            {
                _ = value ?? throw new ArgumentNullException(nameof(value));
                var header = string.Join(", ", value);
                SetResetLock(true);
                HeaderParameter["Accept-Encoding"] = header;
                fieldAcceptEncoding = new Lazy<ReadOnlyCollection<string>>(value);
                SetResetLock(false);
            }
        }
        
        private Lazy<HttpConnectionType>? fieldConnection = null;
        public HttpConnectionType FieldConnection
        {
            get
            {
                if (fieldConnection != null)
                    return fieldConnection.Value;
                return (fieldConnection = new Lazy<HttpConnectionType>(
                    () => HeaderParameter.TryGetValue("Connection", out string value)
                        && value.ToLower() == "keep-alive" ?
                            HttpConnectionType.KeepAlive :
                            HttpConnectionType.Close
                )).Value;
            }
            set
            {
                SetResetLock(true);
                switch (value)
                {
                    case HttpConnectionType.KeepAlive:
                        HeaderParameter["Connection"] = "keep-alive";
                        break;
                    case HttpConnectionType.Close:
                        HeaderParameter["Connection"] = "close";
                        break;
                    default:
                        SetResetLock(false);
                        throw new NotSupportedException($"Unsupported connection type {value}");
                }
                fieldConnection = new Lazy<HttpConnectionType>(value);
                SetResetLock(false);
            }
        }
        
        private Lazy<HttpCookie>? cookie = null;
        public HttpCookie Cookie
        {
            get
            {
                if (cookie != null)
                    return cookie.Value;
                return (cookie = new Lazy<HttpCookie>(
                    () => HeaderParameter.TryGetValue("Cookie", out string value) ?
                        new HttpCookie(value) :
                        new HttpCookie("")
                )).Value;
            }
        }

        public string? FieldUserAgent
        {
            get => GetHeader("User-Agent");
            set 
            {
                SetResetLock(true);
                SetHeader("User-Agent", value);
                SetResetLock(false);
            }
        }
    }
}
