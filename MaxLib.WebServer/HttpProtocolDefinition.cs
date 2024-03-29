﻿using System;
using System.Linq;

#nullable enable

namespace MaxLib.WebServer
{
    public static class HttpProtocolDefinition
    {
        public const string HttpVersion1_0 = "HTTP/1.0";
        public const string HttpVersion1_1 = "HTTP/1.1";
        public const string HttpVersion2_0 = "HTTP/2";

        public static bool IsSupported(string Version, string[] SupportedVersions)
        {
            _ = Version ?? throw new ArgumentNullException(nameof(Version));
            _ = SupportedVersions ?? throw new ArgumentNullException(nameof(SupportedVersions));

            if (SupportedVersions.Length == 0) return false;
            if (SupportedVersions.Contains(Version)) return true;
            var ind = Version.IndexOf('.');
            if (ind != -1) Version = Version.Remove(ind);
            for (int i = 0; i < SupportedVersions.Length; ++i)
                if (SupportedVersions[i].StartsWith(Version)) return true;
            return false;
        }
    }
}
