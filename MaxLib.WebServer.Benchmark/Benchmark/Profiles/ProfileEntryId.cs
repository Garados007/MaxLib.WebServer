namespace MaxLib.WebServer.Benchmark.Profiles
{
    public class ProfileEntryId : ProfileId<(string type, string info)>
    {
        public ProfileEntryId(string type, string info, int increment) 
            : base((type, info), increment)
        {
        }
    }
}