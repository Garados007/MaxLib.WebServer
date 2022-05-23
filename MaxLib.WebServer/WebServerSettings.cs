using MaxLib.Ini;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

#nullable enable

namespace MaxLib.WebServer
{
    public class WebServerSettings
    {
        public int Port { get; private set; }

        public int ConnectionTimeout { get; private set; }

        IPAddress ipFilter = IPAddress.Any;
        public IPAddress IPFilter
        {
            get => ipFilter;
            set => ipFilter = value ?? throw new ArgumentNullException(nameof(IPFilter));
        }

        /// <summary>
        /// Specify where the monitoring output should be written to. Set it to <c>null</c> to
        /// disable monitoring (this is the default setting).
        /// </summary>
        public string? MonitoringOutputDirectory { get; set; }

        /// <summary>
        /// Specify how the monitoring output should be formated.
        /// </summary>
        public Monitoring.OutputFormat MonitoringOutputFormat { get; set; }

        TimeSpan connectionDelay = TimeSpan.FromMilliseconds(20);
        /// <summary>
        /// The time after which the server should check for new incomming connections or requests.
        /// Keep it to a reasonable value to reduce "lags" for the user. <br/> If you are building a
        /// high performance server with lowest lag possible set it to <see cref="TimeSpan.Zero" />.
        /// Setting to this is not recommended because this will increase the cpu usage heavily.
        /// <br/> The supported range is 0 ms to 1 second. The default value is 20 ms.
        /// </summary>
        public TimeSpan ConnectionDelay
        {
            get => connectionDelay;
            set
            {
                if (value < TimeSpan.Zero || value > TimeSpan.FromSeconds(1))
                    throw new ArgumentOutOfRangeException(nameof(ConnectionDelay), value, "supported range is 0ms to 1s");
                connectionDelay = value;
            }
        }

        //Debug
        public bool Debug_WriteRequests = false;
        public bool Debug_LogConnections = false;

        public Dictionary<string, string> DefaultFileMimeAssociation { get; } 
            = new Dictionary<string, string>();

        protected enum SettingTypes
        {
            MimeAssociation,
            ServerSettings
        }

        public string? SettingsPath { get; private set; }

        public virtual void LoadSettingFromData(string data)
        {
            _ = data ?? throw new ArgumentNullException(nameof(data));
            var sf = new Ini.Parser.IniParser().ParseFromString(data);
            if (sf.GetGroup("Mime") != null)
                Load_Mime(sf);
            if (sf.GetGroup("Server") != null)
                Load_Server(sf);
        }

        public virtual void LoadSetting(string path)
        {
            _ = path ?? throw new ArgumentNullException(nameof(path));
            SettingsPath = path;
            var sf = new Ini.Parser.IniParser().Parse(path);
            if (sf.GetGroup("Mime") != null)
                Load_Mime(sf);
            if (sf.GetGroup("Server") != null)
                Load_Server(sf);
        }

        protected virtual void Load_Mime(IniFile set)
        {
            DefaultFileMimeAssociation.Clear();
            var gr = set.GetGroup("Mime").GetAll();
            foreach (IniOption keypair in gr)
                DefaultFileMimeAssociation[keypair.Name] = keypair.String;
        }

        protected virtual void Load_Server(IniFile set)
        {
            var server = set.GetGroup("Server");
            Port = server.GetInt32("Port", 80);
            if (Port <= 0 || Port >= 0xffff)
                Port = 80;
            ConnectionTimeout = server.GetInt32("ConnectionTimeout", 2000);
            if (ConnectionTimeout < 0)
                ConnectionTimeout = 2000;
        }

        public WebServerSettings(string settingFolderPath)
        {
            _ = settingFolderPath ?? throw new ArgumentNullException(nameof(settingFolderPath));
            if (Directory.Exists(settingFolderPath))
                foreach (var file in Directory.GetFiles(settingFolderPath))
                {
                    if (file.EndsWith(".ini"))
                        LoadSetting(file);
                }
            else if (File.Exists(settingFolderPath))
                LoadSetting(settingFolderPath);
            else throw new DirectoryNotFoundException();
        }

        public WebServerSettings(int port, int connectionTimeout)
        {
            if (port <= 0 || port >= 0xffff)
                throw new ArgumentOutOfRangeException(nameof(port));
            if (connectionTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(connectionTimeout));
            Port = port;
            ConnectionTimeout = connectionTimeout;
        }
    }
}
