using System.IO;

namespace MaxLib.WebServer.Builder.Debugger.Example.Properties
{
    public static class Resources
    {
        private static string? debugger_IndexShell;

        /// <summary>
        /// The self-contained HTML page <see cref="IndexService" /> serves at "/".
        /// </summary>
        public static string Debugger_IndexShell
        {
            get
            {
                if (debugger_IndexShell != null)
                    return debugger_IndexShell;
                var assembly = typeof(Resources).Assembly;
                using var stream = assembly.GetManifestResourceStream(
                    "MaxLib.WebServer.Builder.Debugger.Example.Resources.Debugger.IndexShell.html"
                )!;
                using var reader = new StreamReader(stream);
                return debugger_IndexShell = reader.ReadToEnd();
            }
        }
    }
}
