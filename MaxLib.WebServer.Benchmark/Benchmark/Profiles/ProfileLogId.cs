using System;

namespace MaxLib.WebServer.Benchmark.Profiles
{
    public class ProfileLogId : ProfileId<(ProfileEntryId entry, string format)>
    {
        public ProfileLogId(ProfileEntryId entry, string format, int increment)
            : base((entry, format), increment)
        {
        }
    }
}