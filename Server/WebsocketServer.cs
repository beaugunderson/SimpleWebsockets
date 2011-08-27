/*
Portions copyright 2011 Beau Gunderson - http://www.beaugunderson.com/
Portions copyright 2011 Olivine Labs, LLC. - http://www.olivinelabs.com/
*/

/*
This file is part of SimpleWebsockets.

SimpleWebsockets is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

SimpleWebsockets is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License
along with SimpleWebsockets.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using SimpleWebsockets.Server.Classes;
using SimpleWebsockets.Server.Handlers.WebSocket;

using log4net;

namespace SimpleWebsockets.Server
{
    public delegate void OnEventDelegate(UserContext context);

    /// <summary>
    /// The Main WebSocket Server
    /// </summary>
    public class WebsocketServer : IDisposable
    {
        /// <summary>
        /// This Semaphore protects out clients variable on increment/decrement when a user connects/disconnects.
        /// </summary>
        private readonly SemaphoreSlim _clientLock = new SemaphoreSlim(1);

        private const int DefaultBufferSize = 512;
        private TcpListener _listener;

        /// <summary>
        /// This Semaphore limits how many connection events we have active at a time.
        /// </summary>
        private readonly SemaphoreSlim _connectReady = new SemaphoreSlim(10);

        private string _originHost = String.Empty;
        private string _destinationHost = String.Empty;

        /// <summary>
        /// These are the default OnEvent delegates for the server. By default, all new UserContexts will use these events.
        /// It is up to you whether you want to replace them at runtime or even manually set the events differently per connection in OnReceive.
        /// </summary>
        public OnEventDelegate DefaultOnConnect = x => {};
        public OnEventDelegate DefaultOnDisconnect = x => {};
        public OnEventDelegate DefaultOnReceive = x => {};
        public OnEventDelegate DefaultOnSend = x => {};

        /// <summary>
        /// This is the Flash Access Policy Server. It allows us to facilitate flash socket connections much more quickly in most cases.
        /// Don't mess with it through here. It's only public so we can access it later from all the IOCPs.
        /// </summary>
        public AccessPolicyServer AccessPolicyServer;

        /// <summary>
        /// 
        /// </summary>
        public ILog Log = LogManager.GetLogger("SimpleWebsockets.Log");

        /// <summary>
        /// Configuration for the above heartbeat setup.
        /// TimeOut : How long until a connection drops when it doesn't receive anything.
        /// </summary>
        public TimeSpan TimeOut = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Enables or disables the Flash Access Policy Server(AccessPolicyServer).
        /// This is used when you would like your app to only listen on a single port rather than 2.
        /// Warning, any flash socket connections will have an added delay on connection due to the client looking to port 843 first for the connection restrictions.
        /// </summary>
        public bool FlashAPEnabled = true;

        /// <summary>
        /// Gets the client count.
        /// </summary>
        public int ClientCount { get; private set; }

        /// <summary>
        /// Gets or sets the port.
        /// </summary>
        /// <value>
        /// The port.
        /// </value>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the listener address.
        /// </summary>
        /// <value>
        /// The listener address.
        /// </value>
        public IPAddress ListenerAddress { get; set; }

        /// <summary>
        /// Gets or sets the origin host.
        /// </summary>
        /// <value>
        /// The origin host.
        /// </value>
        public string OriginHost
        {
            get
            {
                return _originHost;
            }

            set
            {
                _originHost = value;

                WebSocketAuthentication.Origin = _originHost;
            }
        }

        /// <summary>
        /// Gets or sets the destination host.
        /// </summary>
        /// <value>
        /// The destination host.
        /// </value>
        public string DestinationHost
        {
            get
            {
                return _destinationHost;
            }
            set
            {
                _destinationHost = value;

                WebSocketAuthentication.Location = _destinationHost;
            }
        }

        /// <summary>
        /// Sets the name of the logger.
        /// </summary>
        /// <value>
        /// The name of the logger.
        /// </value>
        public string LoggerName
        {
            set
            {
                Log = LogManager.GetLogger(value);
            }
        }

        /// <summary>
        /// Sets the log config file name.
        /// </summary>
        /// <value>
        /// The log config file name.
        /// </value>
        public static string LogConfigFile
        {
            set
            {
                log4net.Config.XmlConfigurator.Configure(new FileInfo(value));
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebsocketServer"/> class.
        /// </summary>
        /// <param name="listenPort">The listen port.</param>
        /// <param name="listenIp">The listen ip.</param>
        public WebsocketServer(int listenPort = 0, IPAddress listenIp = null)
        {
            ListenerAddress = IPAddress.Any;
            Port = 81;
            LogConfigFile = "SimpleWebsockets.config";
            LoggerName = "SimpleWebsockets.Log";
            
            if (listenPort > 0)
            {
                Port = listenPort;
            }
            
            if (listenIp != null)
            {
                ListenerAddress = listenIp;
            }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public void Start()
        {
            if (_listener == null)
            {
                try
                {
                    AccessPolicyServer = new AccessPolicyServer(ListenerAddress, OriginHost, Port);

                    if (FlashAPEnabled)
                    {
                        AccessPolicyServer.Start();
                    }

                    _listener = new TcpListener(ListenerAddress, Port);

                    ThreadPool.QueueUserWorkItem(Listen, null);
                }
                catch
                {
                     /* Ignore */
                }
            }

            Log.Info("SimpleWebsockets server started");
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public void Stop()
        {
            if (_listener != null)
            {
                try
                {
                    _listener.Stop();
            
                    if (AccessPolicyServer != null && FlashAPEnabled)
                    {
                        AccessPolicyServer.Stop();
                    }
                }
                catch
                {
                     /* Ignore */
                }
            }

            _listener = null;
            AccessPolicyServer = null;
            
            Log.Info("SimpleWebsockets server stopped");
        }

        /// <summary>
        /// Restarts this instance.
        /// </summary>
        public void Restart()
        {
            Stop();
            Start();
        }

        /// <summary>
        /// Listens for new connections.
        /// Utilizes a semaphore(ConnectReady) to manage how many active connect attempts we can manage concurrently.
        /// </summary>
        /// <param name="state">The state.</param>
        private void Listen(object state)
        {
            _listener.Start();

            while (_listener != null)
            {
                try
                {
                    _listener.BeginAcceptTcpClient(RunClient, null);

                    _connectReady.Wait();
                }
                catch
                {
                    /* Ignore */
                }
            }
        }

        /// <summary>
        /// Runs the client.
        /// Sets up the UserContext.
        /// Executes in it's own thread.
        /// Utilizes a semaphore(ReceiveReady) to limit the number of receive events active for this client to 1 at a time.
        /// </summary>
        /// <param name="result">The A result.</param>
        private void RunClient(IAsyncResult result)
        {
            TcpClient connection = null;

            try
            {
                if (_listener != null)
                {
                    connection = _listener.EndAcceptTcpClient(result);
                }
            }
            catch (Exception e)
            {
                Log.Debug("Connect failed", e);
            }

            _connectReady.Release();

            if (connection == null)
            {
                return;
            }

            _clientLock.Wait();
            ClientCount++;
            _clientLock.Release();

            using (var context = new Context())
            {
                context.Server = this;
                context.Connection = connection;
                context.BufferSize = DefaultBufferSize;

                context.UserContext.ClientAddress = context.Connection.Client.RemoteEndPoint;

                context.UserContext.SetOnConnect(DefaultOnConnect);
                context.UserContext.SetOnDisconnect(DefaultOnDisconnect);
                context.UserContext.SetOnSend(DefaultOnSend);
                context.UserContext.SetOnReceive(DefaultOnReceive);
                
                context.UserContext.OnConnect();

                try
                {
                    while (context.Connection.Connected)
                    {
                        if (context.ReceiveReady.Wait(TimeOut))
                        {
                            context.Connection.Client.BeginReceive(context.Buffer, 0, context.Buffer.Length, SocketFlags.None, DoReceive, context);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Debug("Client Forcefully Disconnected", e);
                }
            }

            _clientLock.Wait();
            ClientCount--;
            _clientLock.Release();
        }

        /// <summary>
        /// The root receive event for each client. Executes in it's own thread.
        /// </summary>
        /// <param name="result">The Async result.</param>
        private void DoReceive(IAsyncResult result)
        {
            var context = (Context)result.AsyncState;

            context.Reset();
            
            try
            {
                context.ReceivedByteCount = context.Connection.Client.EndReceive(result);
            }
            catch (Exception e)
            { 
                Log.Debug("Client Forcefully Disconnected", e); 
            }

            if (context.ReceivedByteCount > 0)
            {
                context.ReceiveReady.Release();
                context.Handler.HandleRequest(context);
            }
            else
            {
                context.Dispose();
                context.ReceiveReady.Release();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}