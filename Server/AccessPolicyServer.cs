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
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SimpleWebsockets.Server
{
    /// <summary>
    /// This is the Flash Access Policy Server
    /// It manages sending the XML cross domain policy to flash socket clients over port 843.
    /// See http://www.adobe.com/devnet/articles/crossdomain_policy_file_spec.html for details.
    /// </summary>
    public class AccessPolicyServer : IDisposable
    {
        private int _port = 843;
        private IPAddress _listenerAddress = IPAddress.Any;
        private string _allowedHost = "localhost";
        private int _allowedPort = 81;

        private TcpListener _listener;

        /// <summary>
        /// Limits how many active connect events we have.
        /// </summary>
        private readonly SemaphoreSlim ConnectReady = new SemaphoreSlim(10);

        /// <summary>
        /// The pre-formatted XML response.
        /// </summary>
        private const string Response = 
            "<cross-domain-policy>\r\n" +
                "\t<allow-access-from domain=\"{0}\" to-ports=\"{1}\" />\r\n" +
            "</cross-domain-policy>\r\n\0";

        /// <summary>
        /// Gets or sets the listener address.
        /// </summary>
        /// <value>
        /// The listener address.
        /// </value>
        public IPAddress ListenerAddress
        {
            get
            {
                return _listenerAddress;
            }

            set
            {
                _listenerAddress = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessPolicyServer"/> class.
        /// </summary>
        /// <param name="listenAddress">The listen address.</param>
        /// <param name="originDomain">The origin domain.</param>
        /// <param name="allowedPort">The allowed port.</param>
        public AccessPolicyServer(IPAddress listenAddress, string originDomain, int allowedPort)
        {
            string originLockdown = "*";

            if (originDomain != String.Empty)
            {
                originLockdown = originDomain;
            }

            _listenerAddress = listenAddress;
            _allowedHost = originLockdown;
            _allowedPort = allowedPort;
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public void Start()
        {
            if (_listener != null)
            {
                return;
            }
            
            try
            {
                _listener = new TcpListener(ListenerAddress, _port);

                ThreadPool.QueueUserWorkItem(Listen, null);
            }
            catch { /* Ignore */ }
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
                }
                catch { /* Ignore */ }
            }

            _listener = null;
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
        /// Listens on the ip and port specified.
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
                }
                catch { /* Ignore */ }

                ConnectReady.Wait();
            }
        }

        /// <summary>
        /// Runs the client.
        /// </summary>
        /// <param name="result">The Async result.</param>
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
            catch { /* Ignore */ }

            ConnectReady.Release();

            if (connection == null)
            {
                return;
            }
            
            try
            {
                connection.Client.Receive(new byte[32]);
            
                SendResponse(connection);
                
                connection.Client.Close();
            }
            catch { /* Ignore */ }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Sends the response.
        /// </summary>
        /// <param name="connection">The TCP Connection.</param>
        public void SendResponse(TcpClient connection)
        {
            connection.Client.Send(Encoding.UTF8.GetBytes(String.Format(Response, _allowedHost, _allowedPort)));
        }
    }
}