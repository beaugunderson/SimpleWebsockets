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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using SimpleWebsockets.Server.Handlers;

namespace SimpleWebsockets.Server.Classes
{
    /// <summary>
    /// Contains data we will export to the Event Delegates.
    /// </summary>
    public class UserContext
    {
        /// <summary>
        /// AQ Link to the parent User Context
        /// </summary>
        private readonly Context _context;
        
        /// <summary>
        /// The Data Frame that this client is currently processing.
        /// </summary>
        public readonly DataFrame DataFrame = new DataFrame();
        
        /// <summary>
        /// What character encoding to use.
        /// </summary>
        public readonly UTF8Encoding Encoding = new UTF8Encoding();
        
        /// <summary>
        /// User defined data. Can be anything.
        /// </summary>
        public Object Data;

        /// <summary>
        /// The path of this request.
        /// </summary>
        public string RequestPath = "/";
        
        /// <summary>
        /// The remote endpoint address.
        /// </summary>
        public EndPoint ClientAddress;
        
        /// <summary>
        /// The type of connection this is
        /// </summary>
        public Protocol Protocol = Protocol.None;
        
        /// <summary>
        /// OnEvent Delegates specific to this connection.
        /// </summary>
        private OnEventDelegate _onConnect = x => {};
        private OnEventDelegate _onDisconnect = x => {};
        private OnEventDelegate _onReceive = x => {};
        private OnEventDelegate _onSend = x => {};

        public readonly Header Header;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserContext"/> class.
        /// </summary>
        /// <param name="context">The user context.</param>
        public UserContext(Context context)
        {
            _context = context;

            Header = _context.Header;
        }

        /// <summary>
        /// Called when [connect].
        /// </summary>
        public void OnConnect()
        {
            try
            {
                _onConnect(this);
            }
            catch (Exception e)
            {
                _context.Server.Log.Error("Fatal Error in user specified OnConnect", e);
            }
        }

        /// <summary>
        /// Called when [disconnect].
        /// </summary>
        public void OnDisconnect()
        {
            try
            {
                _context.Connected = false;
                _onDisconnect(this);
            }
            catch (Exception e)
            {
                _context.Server.Log.Error("Fatal Error in user specified OnDisconnect", e);
            }
        }

        /// <summary>
        /// Called when [send].
        /// </summary>
        public void OnSend()
        {
            try
            {
                _onSend(this);
            }
            catch (Exception e)
            {
                _context.Server.Log.Error("Fatal Error in user specified OnSend", e);
            }
        }

        /// <summary>
        /// Called when [receive].
        /// </summary>
        public void OnReceive()
        {
            try
            {
                _onReceive(this);
            }
            catch (Exception e)
            {
                _context.Server.Log.Error("Fatal Error in user specified OnReceive", e);
            }
        }

        /// <summary>
        /// Sets the on connect event.
        /// </summary>
        /// <param name="ADelegate">The Event Delegate.</param>
        public void SetOnConnect(OnEventDelegate ADelegate)
        {
            _onConnect = ADelegate;
        }

        /// <summary>
        /// Sets the on disconnect event.
        /// </summary>
        /// <param name="ADelegate">The Event Delegate.</param>
        public void SetOnDisconnect(OnEventDelegate ADelegate)
        {
            _onDisconnect = ADelegate;
        }

        /// <summary>
        /// Sets the on send event.
        /// </summary>
        /// <param name="ADelegate">The Event Delegate.</param>
        public void SetOnSend(OnEventDelegate ADelegate)
        {
            _onSend = ADelegate;
        }

        /// <summary>
        /// Sets the on receive event.
        /// </summary>
        /// <param name="ADelegate">The Event Delegate.</param>
        public void SetOnReceive(OnEventDelegate ADelegate)
        {
            _onReceive = ADelegate;
        }

        /// <summary>
        /// Sends the specified data.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="close">if set to <c>true</c> [close].</param>
        public void Send(string data, bool close = false)
        {
            Send(Encoding.GetBytes(data), close);
        }

        /// <summary>
        /// Sends the specified data.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="close">if set to <c>true</c> [close].</param>
        public void Send(byte[] data, bool close = false)
        {
            _context.Handler.Send(data, _context, close);
        }

        /// <summary>
        /// Sends raw data.
        /// </summary>
        /// <param name="data">The data.</param>
        public void SendRaw(byte[] data)
        {
            DefaultHandler.Instance.Send(data, _context);
        }
    }

    /// <summary>
    /// This class contains the required data for each connection to the server.
    /// </summary>
    public class Context : IDisposable
    {
        /// <summary>
        /// The exported version of this context.
        /// </summary>
        public readonly UserContext UserContext;

        /// <summary>
        /// The raw client connection.
        /// </summary>
        public TcpClient Connection { get; set; }

        /// <summary>
        /// Whether or not the TCPClient is still connected.
        /// </summary>
        public bool Connected = true;
        
        /// <summary>
        /// The buffer used for accepting raw data from the socket.
        /// </summary>
        public byte[] Buffer;
        
        /// <summary>
        /// How many bytes we received this tick.
        /// </summary>
        public int ReceivedByteCount;
        
        /// <summary>
        /// Whether or not this client has passed all the setup routines for the current handler(authentication, etc)
        /// </summary>
        public Boolean IsSetup;
        
        /// <summary>
        /// The current connection handler.
        /// </summary>
        public Handler Handler = DefaultHandler.Instance;

        /// <summary>
        /// Semaphores that limit sends and receives to 1 and a time.
        /// </summary>
        public readonly SemaphoreSlim ReceiveReady = new SemaphoreSlim(1);
        public readonly SemaphoreSlim SendReady = new SemaphoreSlim(1);
        
        /// <summary>
        /// A link to the server listener instance this client is currently hosted on.
        /// </summary>
        public WebsocketServer Server;

        /// <summary>
        /// The Header
        /// </summary>
        public Header Header;

        private int _bufferSize = 512;

        /// <summary>
        /// Gets or sets the size of the buffer.
        /// </summary>
        /// <value>
        /// The size of the buffer.
        /// </value>
        public int BufferSize
        {
            get
            {
                return _bufferSize;
            }

            set
            {
                _bufferSize = value;

                Buffer = new byte[_bufferSize];
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Context"/> class.
        /// </summary>
        public Context()
        {
            Buffer = new byte[_bufferSize];

            UserContext = new UserContext(this);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Connection.Client.Close();
                Connection = null;
            }
            catch (Exception e)
            {
                Server.Log.Debug("Client Already Disconnected", e);
            }
            finally
            {
                if (Connected)
                {
                    Connected = false;

                    UserContext.OnDisconnect();
                }
            }
        }

        /// <summary>
        /// Resets this instance.
        /// Clears the dataframe if necessary. Resets Received byte count.
        /// </summary>
        public void Reset()
        {
            if (UserContext.DataFrame.State == DataFrame.DataState.Complete)
            {
                UserContext.DataFrame.Clear();
            }

            ReceivedByteCount = 0;
        }
    }
}
