namespace MaxLib.WebServer.IO
{
    [System.Serializable]
    public class ReadLineOverflowException : System.Exception
    {
        public HttpStateCode State { get; set; } = HttpStateCode.InternalServerError;

        public ReadLineOverflowException() { }
        public ReadLineOverflowException(HttpStateCode state)
        {
            State = state;
        }
        public ReadLineOverflowException(HttpStateCode state, string message) : base(message)
        {
            State = state;
        }
    }
}
