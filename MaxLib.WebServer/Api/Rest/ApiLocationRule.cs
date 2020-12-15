using System;

namespace MaxLib.WebServer.Api.Rest
{
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
