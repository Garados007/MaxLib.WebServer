using System;

namespace MaxLib.WebServer.Api.Rest
{
    public abstract class ApiGetRule : ApiRule
    {
        private string key;
        public string Key
        {
            get => key;
            set => key = value ?? throw new ArgumentNullException(nameof(Key));
        }
    }
}
