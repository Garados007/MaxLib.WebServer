using System;

#nullable enable

namespace MaxLib.WebServer.Api.Rest
{
    [Obsolete("The ApiService and the RestApiService classes are no longer maintained and will be removed in a future update. Use the Builder system instead.")]
    public abstract class ApiLocationRule : ApiRule
    {
        private int index = 0;
        public int Index
        {
            get => index;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(Index));
                index = value;
            }
        }
    }
}
