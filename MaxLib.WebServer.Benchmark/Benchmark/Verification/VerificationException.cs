namespace MaxLib.WebServer.Benchmark.Verification
{
    [System.Serializable]
    public class VerificationException : System.Exception
    {
        public VerificationException() { }
        public VerificationException(string path, object? expected, object? actual)
            : base($"Invalid value at [{path}]: {(expected == null ? "<null>" : $"{{{expected}}}")} <==> {(actual == null ? "<null>" : $"{{{actual}}}")}")
        {}
        public VerificationException(string message) : base(message) { }
        public VerificationException(string message, System.Exception inner) : base(message, inner) { }
        protected VerificationException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}