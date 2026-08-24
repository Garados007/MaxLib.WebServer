using System.IO;

#nullable enable

namespace MaxLib.WebServer.Properties
{
    public static class Resources
    {
        private static string? files_ViewHtmlCss;

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

        private static string? debugger_Shell;

        /// <summary>
        /// The self-contained HTML/CSS/JS shell <see cref="Builder.Debugger.DebuggerService" />
        /// serves at its base path.
        /// </summary>
        public static string Debugger_Shell
        {
            get
            {
                if (debugger_Shell != null)
                    return debugger_Shell;
                var assembly = typeof(Resources).Assembly;
                using var stream = assembly.GetManifestResourceStream("MaxLib.WebServer.Resources.Debugger.Shell.html")!;
                using var reader = new StreamReader(stream);
                return debugger_Shell = reader.ReadToEnd();
            }
        }
    }
}
