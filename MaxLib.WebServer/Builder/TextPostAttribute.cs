using System;
using System.Collections.Generic;
using System.IO;
using MaxLib.WebServer.Builder.Tools;

namespace MaxLib.WebServer.Builder
{
    public class TextPostAttribute : ParamAttributeBase
    {
        public override Type Type  => typeof(string);

        public override Result<object?> GetValue(WebProgressTask task, string field, Dictionary<string, object?> vars)
        {
            var post = task.Request.Post.Data;
            if (!(post is MaxLib.WebServer.Post.UnknownPostData data))
                return new Result<object?>();
            using var reader = new StreamReader(
                data.Data,
                System.Text.Encoding.UTF8,
                bufferSize: -1,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true
            );
            return new Result<object?>(reader.ReadToEnd());
        }
    }
}