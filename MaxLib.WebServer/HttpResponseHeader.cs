using System;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpResponseHeader : HttpHeader
    {
        public HttpStateCode StatusCode { get; set; } = HttpStateCode.OK;

        public string? FieldLocation
        {
            get => GetHeader("Location");
            set => SetHeader("Location", value);
        }

        public string? FieldDate
        {
            get => GetHeader("Date");
            set => SetHeader("Date", value);
        }

        public string? FieldLastModified
        {
            get => GetHeader("Last-Modified");
            set => SetHeader("Last-Modified", value);
        }

        public string? FieldContentType
        {
            get => GetHeader("Content-Type");
            set => SetHeader("Content-Type", value);
        }

        public virtual void SetActualDate()
        {
            FieldDate = WebServerUtils.GetDateString(DateTime.UtcNow);
        }
    }
}
