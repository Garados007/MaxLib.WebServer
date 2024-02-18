using System;

#nullable enable

namespace MaxLib.WebServer.Api.Rest
{
    [Obsolete("The ApiService and the RestApiService classes are no longer maintained and will be removed in a future update. Use the Builder system instead.")]
    public abstract class ApiGetRule : ApiRule
    {
        private string? key;
        public string? Key
        {
            get => key;
            set => key = value ?? throw new ArgumentNullException(nameof(Key));
        }
    }
}
