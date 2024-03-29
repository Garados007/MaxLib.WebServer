﻿using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.SSL
{
    public class SecureWebServer : Server
    {
        public SecureWebServerSettings SecureSettings => (SecureWebServerSettings)Settings;

        //Secure Server
        protected TcpListener? SecureListener;
        protected Thread? SecureServerThread;

        public SecureWebServer(SecureWebServerSettings settings) : base(settings)
        {
        }

        public override void Start()
        {
            if (SecureSettings.EnableUnsafePort)
                base.Start();
            WebServerLog.Add(ServerLogType.Information, GetType(), "StartUp", "Start Secure Server on Port {0}", SecureSettings.SecurePort);
            ServerExecution = true;
            SecureListener = new TcpListener(new IPEndPoint(Settings.IPFilter, SecureSettings.SecurePort));
            SecureListener.Start();
            SecureServerThread = new Thread(SecureMainTask)
            {
                Name = "SecureServerThread - Port: " + SecureSettings.SecurePort.ToString()
            };
            SecureServerThread.Start();
        }

        public override void Stop()
        {
            if (SecureSettings.EnableUnsafePort)
                base.Stop();
            WebServerLog.Add(ServerLogType.Information, GetType(), "StartUp", "Stopped Secure Server");
            ServerExecution = false;
        }

        protected virtual void SecureMainTask()
        {
            WebServerLog.Add(ServerLogType.Information, GetType(), "StartUp", "Secure Server succesfuly started");
            var watch = new Stopwatch();
            while (ServerExecution)
            {
                watch.Restart();
                //pending connection
                int step = 0;
                for (; step < 10; step++)
                {
                    if (!SecureListener!.Pending()) break;
                    SecureClientConnected(SecureListener.AcceptTcpClient());
                }
                //wait
                if (SecureListener!.Pending()) 
                    continue;
                var time = watch.ElapsedMilliseconds % 20;
                Thread.Sleep(20 - (int)time);
            }
            watch.Stop();
            SecureListener!.Stop();
            WebServerLog.Add(ServerLogType.Information, GetType(), "StartUp", "Secure Server succesfuly stopped");
        }

        protected virtual void SecureClientConnected(TcpClient client)
        {
            if (SecureSettings.Certificate == null)
            {
                client.Close();
                return;
            }
            //prepare session
            var connection = new HttpConnection()
            {
                NetworkClient = client,
                Ip = client.Client.RemoteEndPoint is IPEndPoint iPEndPoint
                    ? iPEndPoint.Address.ToString()
                    : client.Client.RemoteEndPoint?.ToString(),
            };
            AllConnections.Add(connection);
            //listen to connection
            _ = Task.Run(async () =>
            {
                //authentificate as server and establish ssl connection
                var stream = new SslStream(client.GetStream(), false);
                connection.NetworkStream = stream;
                stream.AuthenticateAsServer(
                    serverCertificate:          SecureSettings.Certificate, 
                    clientCertificateRequired:  false, 
                    enabledSslProtocols:        SslProtocols.None, 
                    checkCertificateRevocation: true
                    );
                if (!stream.IsAuthenticated)
                {
                    stream.Dispose();
                    client.Close();
                    AllConnections.Remove(connection);
                    return;
                }

                await SafeClientStartListen(connection).ConfigureAwait(false);
            });
        }
    }
}
