﻿using System;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Services
{
    /// <summary>
    /// WebServiceType.PostParseRequest: Verarbeitet die Aktion HEAD oder OPTIONS, die vom Browser angefordert wurde
    /// </summary>
    public class HttpHeaderSpecialAction : WebService
    {
        /// <summary>
        /// WebServiceType.PostParseRequest: Verarbeitet die Aktion HEAD oder OPTIONS, die vom Browser angefordert wurde
        /// </summary>
        public HttpHeaderSpecialAction() : base(ServerStage.ParseRequest) { }

        public override async Task ProgressTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));

            switch (task.Request.ProtocolMethod)
            {
                case HttpProtocollMethod.Head:
                    task.Document.Information["Only Header"] = true;
                    break;
                case HttpProtocollMethod.Options:
                    {
                        var source = new HttpStringDataSource("GET\r\nPOST\r\nHEAD\r\nOPTIONS\r\nTRACE")
                        {
                            MimeType = MimeType.TextPlain,
                            TransferCompleteData = true
                        };
                        task.Document.DataSources.Add(source);
                        task.NextStage = ServerStage.CreateResponse;
                    }
                    break;
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public override bool CanWorkWith(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));

            switch (task.Request.ProtocolMethod)
            {
                case HttpProtocollMethod.Head: return true;
                case HttpProtocollMethod.Options: return true;
                default: return false;
            }
        }
    }
}
