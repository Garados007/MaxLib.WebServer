using System;
namespace MaxLib.WebServer
{
    [Obsolete("Use WebServicePriority instead")]
    public enum WebProgressImportance : int
    {
        God = WebServicePriority.God,
        VeryHigh = WebServicePriority.VeryHigh,
        High = WebServicePriority.High,
        Normal = WebServicePriority.Normal,
        Low = WebServicePriority.Low,
        VeryLow = WebServicePriority.VeryLow,
    }
}
