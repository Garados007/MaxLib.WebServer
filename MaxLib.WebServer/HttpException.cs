using System;

#nullable enable

namespace MaxLib.WebServer
{
    /// <summary>
    /// Cancel the operation of the single <see cref="WebService.ProgressTask(WebProgressTask)" />.
    /// The engine will automatically set the resulting status to the <see cref="WebProgressTask"
    /// />. These exceptions are intended to simplify the control flow. These messages are not
    /// logged to the output.
    /// </summary>
    [System.Serializable]
    public class HttpException : System.Exception
    {
        public HttpException() { }
        public HttpException(string message) : base(message) 
        {
            DataSource = new HttpStringDataSource(message);
        }

        public HttpException(string message, System.Exception inner) : base(message, inner) 
        { 
            DataSource = new HttpStringDataSource(message);
        }

        public HttpException(HttpStateCode code)
        {
            StateCode = code;
        }

        public HttpException(HttpStateCode code, HttpDataSource dataSource)
        {
            StateCode = code;
            DataSource = dataSource;
        }

        public HttpException(HttpDataSource dataSource)
        {
            DataSource = dataSource;
        }

        protected HttpException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }

        public HttpStateCode StateCode { get; set; } = HttpStateCode.InternalServerError;

        public HttpDataSource? DataSource { get; set; }
    }
}