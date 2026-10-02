using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#nullable enable

namespace MaxLib.WebServer.SSL
{
    public class SecureWebServer : Server
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<SecureWebServer>();
        static readonly EventId StartUpEventId = new(0, "StartUp");
        static readonly EventId HandshakeEventId = new(0, "Handshake");
        static readonly EventId UnhandledExceptionEventId = new(0, "Unhandled Exception");

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
            logger.LogInformation(StartUpEventId, "Start Secure Server on Port {Port}", SecureSettings.SecurePort);
            ServerExecution = true;
            SecureListener = new TcpListener(new IPEndPoint(Settings.IPFilter, SecureSettings.SecurePort));
            SecureListener.Start();
            SecureServerThread = new Thread(SecureMainTask)
            {
                Name = "SecureServerThread - Port: " + SecureSettings.SecurePort.ToString(CultureInfo.InvariantCulture)
            };
            SecureServerThread.Start();
        }

        public override void Stop()
        {
            if (SecureSettings.EnableUnsafePort)
                base.Stop();
            logger.LogInformation(StartUpEventId, "Stopped Secure Server");
            ServerExecution = false;
        }

        protected virtual void SecureMainTask()
        {
            logger.LogInformation(StartUpEventId, "Secure Server successfully started");
            var watch = new Stopwatch();
            while (ServerExecution)
            {
                watch.Restart();
                //pending connection
                int step = 0;
                for (; step < 10; step++)
                {
                    if (!SecureListener!.Pending()) break;
                    TcpClient client;
                    try
                    {
                        client = SecureListener.AcceptTcpClient();
                    }
                    catch (Exception e)
                    {
                        // like Server.ServerMainTask: this runs on a raw Thread, where an unhandled exception would take
                        // down the process. Nothing was accepted, so there is nothing to clean up.
                        logger.LogCritical(UnhandledExceptionEventId, e, "Unhandled exception while accepting a connection");
                        continue;
                    }
                    try
                    {
                        SecureClientConnected(client);
                    }
                    catch (Exception e)
                    {
                        // SecureClientConnected failed before registering the client in AllConnections;
                        // close it, or its socket leaks
                        client.Close();
                        logger.LogCritical(UnhandledExceptionEventId, e, "Unhandled exception while accepting a connection");
                    }
                }
                //wait
                if (SecureListener!.Pending())
                    continue;
                var time = watch.ElapsedMilliseconds % 20;
                Thread.Sleep(20 - (int)time);
            }
            watch.Stop();
            SecureListener!.Stop();
            logger.LogInformation(StartUpEventId, "Secure Server successfully stopped");
        }

        protected virtual void SecureClientConnected(TcpClient client)
        {
            ArgumentNullException.ThrowIfNull(client);
            if (SecureSettings.Certificate == null)
            {
                client.Close();
                return;
            }
            if (!TryAdmitConnection(client))
                return;
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
                try
                {
                    using var handshakeTimeout = SecureSettings.HandshakeTimeout > TimeSpan.Zero
                        ? new CancellationTokenSource(SecureSettings.HandshakeTimeout)
                        : null;
                    await stream.AuthenticateAsServerAsync(
                        new SslServerAuthenticationOptions
                        {
                            ServerCertificate = SecureSettings.Certificate,
                            ClientCertificateRequired = false,
                            EnabledSslProtocols = SslProtocols.None,
                            CertificateRevocationCheckMode = X509RevocationMode.Online,
                        },
                        handshakeTimeout?.Token ?? CancellationToken.None
                        ).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // a failed handshake (bad ClientHello, disconnect, timeout) leaves stream.IsAuthenticated false,
                    // so the cleanup branch below still runs
                    logger.LogInformation(HandshakeEventId, e,
                        "TLS handshake failed for {RemoteEndPoint}", client.Client.RemoteEndPoint);
                }
                if (!stream.IsAuthenticated)
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                    client.Close();
                    AllConnections.Remove(connection);
                    return;
                }

                await SafeClientStartListen(connection).ConfigureAwait(false);
            });
        }
    }
}
