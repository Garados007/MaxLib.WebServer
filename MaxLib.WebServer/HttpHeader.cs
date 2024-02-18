using System;
using System.Collections.Generic;
using MaxLib.Common.Collections;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public abstract class HttpHeader
    {
        private bool lockReset = false;

        public HttpHeader()
        {
            var param = new ObservableDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            param.CollectionChanged += (_, __) => 
            {
                if (!lockReset)
                    ResetCache();
            };
            HeaderParameter = param;
        }

        /// <summary>
        /// Set the current state of the reset lock. If the reset is locked all changes to
        /// <see cref="HeaderParameter" /> won't call <see cref="ResetCache" /> and therefore
        /// reset the cached state.
        /// </summary>
        /// <param name="lockReset">the new state of the reset lock</param>
        protected internal void SetResetLock(bool lockReset)
        {
            this.lockReset = lockReset;
        }

        protected virtual void ResetCache()
        {

        }

        private string httpProtocol = HttpProtocolDefinition.HttpVersion1_1;
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

        public ObservableDictionary<string, string> HeaderParameter { get; }

        public string? GetHeader(string key)
        {
            _ = key ?? throw new ArgumentNullException(nameof(key));
            return HeaderParameter.TryGetValue(key, out string? value) ? value : null;
        }

        public void SetHeader(IEnumerable<(string, string?)> headers)
        {
            _ = headers ?? throw new ArgumentNullException(nameof(headers));
            SetResetLock(true);
            foreach (var (key, value) in headers)
                if (value != null)
                    HeaderParameter[key] = value;
                else HeaderParameter.Remove(key);
            SetResetLock(false);
            ResetCache();
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

        private string protocolMethod = HttpProtocolMethod.Get;
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
