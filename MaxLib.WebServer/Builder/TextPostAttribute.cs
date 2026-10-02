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
            ArgumentNullException.ThrowIfNull(task);
            var post = task.Request.Post.Data;
            if (post is not MaxLib.WebServer.Post.RawPostData data)
                return new Result<object?>();
            if (data.Entry.Content is ReadOnlyMemory<byte> content)
                return new Result<object?>(data.Encoding.GetString(content.Span));
            if (data.Entry.TempFile is FileInfo tempFile)
                return new Result<object?>(File.ReadAllText(tempFile.FullName, data.Encoding));
            return new Result<object?>("");
        }

        public override string ToString() => "TextPost";
    }
}