﻿using System;
using System.Collections.Generic;

#nullable enable

namespace MaxLib.WebServer.Api.Rest
{
    public class RestQueryArgs
    {
        public string[] Location { get; }

        public Dictionary<string, string> GetArgs { get; }

        public HttpPost Post { get; }

        public Sessions.Session? Session { get; }

        public Dictionary<string, object> ParsedArguments { get; }

        public RestQueryArgs(string[] location, Dictionary<string, string> getArgs, HttpPost post, Sessions.Session? session = null)
        {
            Location = location ?? throw new ArgumentNullException(nameof(location));
            GetArgs = getArgs ?? throw new ArgumentNullException(nameof(getArgs));
            Post = post ?? throw new ArgumentNullException(nameof(post));
            Session = session;
            ParsedArguments = new Dictionary<string, object>();
        }
    }
}
