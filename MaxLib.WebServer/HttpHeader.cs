using System;
using System.Collections.Generic;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public abstract class HttpHeader
    {
        private string httpProtocol = HttpProtocollDefinition.HttpVersion1_1;
        public string HttpProtocol
        {
            get => httpProtocol;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) 
                    throw new ArgumentException("HttpProtocol cannot contain an empty Protocol", nameof(HttpProtocol));
                httpProtocol = value;
            }
        }

        public Dictionary<string, string> HeaderParameter { get; } 
            = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        public string? GetHeader(string key)
        {
            _ = key ?? throw new ArgumentNullException(nameof(key));
            return HeaderParameter.TryGetValue(key, out string value) ? value : null;
        }

        public void SetHeader(IEnumerable<(string, string?)> headers)
        {
            _ = headers ?? throw new ArgumentNullException(nameof(headers));
            foreach (var (key, value) in headers)
                if (value != null)
                    HeaderParameter[key] = value;
                else HeaderParameter.Remove(key);
        }

        public void SetHeader(params (string, string?)[] header)
        {
            _ = header ?? throw new ArgumentNullException(nameof(header));
            SetHeader(headers: header);
        }

        public void SetHeader(string key, string? value)
        {
            _ = key ?? throw new ArgumentNullException(nameof(key));
            if (value != null)
                HeaderParameter[key] = value;
            else HeaderParameter.Remove(key);
        }

        private string protocolMethod = HttpProtocollMethod.Get;
        public string ProtocolMethod
        {
            get => protocolMethod;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) 
                    throw new ArgumentException("ProtocolMethod cannot be empty", nameof(ProtocolMethod));
                protocolMethod = value;
            }
        }
    }
}
