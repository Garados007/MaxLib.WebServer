#nullable enable

using System;

namespace MaxLib.WebServer.Api.Rest
{
    [Obsolete("The ApiService and the RestApiService classes are no longer maintained and will be removed in a future update. Use the Builder system instead.")]
    public abstract class ApiRule
    {
        public bool Required { get; set; } = true;

        public abstract bool Check(RestQueryArgs args);
    }
}
