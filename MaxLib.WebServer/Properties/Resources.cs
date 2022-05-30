using System.IO;

#nullable enable

namespace MaxLib.WebServer.Properties
{
    public static class Resources
    {
        private static string? files_ViewHtmlCss = null;

        public static string Files_ViewHtmlCss
        {
            get 
            {
                if (files_ViewHtmlCss != null)
                    return files_ViewHtmlCss;
                var assembly = typeof(Resources).Assembly;
                using var stream = assembly.GetManifestResourceStream("MaxLib.WebServer.Resources.Files.ViewerHtmlCss.css")!;
                using var reader = new StreamReader(stream);
                return files_ViewHtmlCss = reader.ReadToEnd();
            }
        }
    }
}