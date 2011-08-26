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
using System.Net.Sockets;

using SimpleWebsockets.Server.Classes;
using SimpleWebsockets.Server.Handlers.WebSocket;

namespace SimpleWebsockets.Server.Handlers
{
    /// <summary>
    /// A threadsafe singleton that contains functions which are used to handle incoming connections for the WebSocket Protocol
    /// </summary>
    sealed class WebSocketHandler : Handler
    {
        private static WebSocketHandler _instance;

        private WebSocketHandler() {}

        public static WebSocketHandler Instance
        {
            get 
            {
                CreateLock.Wait();
                
                if (_instance == null)
                {
                    _instance = new WebSocketHandler();
                }
                
                CreateLock.Release();
                
                return _instance;
            }
        }

        /// <summary>
        /// Handles the request.
        /// </summary>
        /// <param name="context">The user context.</param>
        public override void HandleRequest(Context context)
        {
            if (context.IsSetup)
            {
                context.UserContext.DataFrame.Append(context.Buffer, context.ReceivedByteCount);

                if (context.UserContext.DataFrame.State == DataFrame.DataState.Complete)
                {
                    context.UserContext.OnReceive();
                }
            }
            else
            {
                Authenticate(context);
            }
        }

        /// <summary>
        /// Attempts to authenticates the specified user context.
        /// If authentication fails it kills the connection.
        /// </summary>
        /// <param name="context">The user context.</param>
        private void Authenticate(Context context)
        {
            if (WebSocketAuthentication.CheckHandshake(context))
            {
                context.UserContext.Protocol = context.Header.Protocol;
                context.UserContext.RequestPath = context.Header.RequestPath;
                context.Header = null;
                context.IsSetup = true;
            }
            else
            {
                context.Dispose();
            }
        }

        /// <summary>
        /// Sends the specified data.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="context">The user context.</param>
        /// <param name="close">if set to <c>true</c> [close].</param>
        public override void Send(byte[] data, Context context, bool close = false)
        {
            var dataFrameBytes = new DataFrame(data).ToBytes();

            AsyncCallback aCallback = EndSend;

            if (close)
            {
                aCallback = EndSendAndClose;
            }

            context.SendReady.Wait();
            
            try
            {
                context.Connection.Client.BeginSend(dataFrameBytes, 0, dataFrameBytes.Length, SocketFlags.None, aCallback, context);
            }
            catch
            {
                context.SendReady.Release();
            }
        }

        /// <summary>
        /// Ends the send.
        /// </summary>
        /// <param name="result">The Async result.</param>
        public override void EndSend(IAsyncResult result)
        {
            var context = (Context)result.AsyncState;

            try
            {
                context.Connection.Client.EndSend(result);
                context.SendReady.Release();
            }
            catch
            {
                context.SendReady.Release(); 
            }

            context.UserContext.OnSend();
        }

        /// <summary>
        /// Ends the send and closes the connection.
        /// </summary>
        /// <param name="result">The Async result.</param>
        public override void EndSendAndClose(IAsyncResult result)
        {
            var context = (Context)result.AsyncState;

            try
            {
                context.Connection.Client.EndSend(result);
                context.SendReady.Release();
            }
            catch
            {
               context.SendReady.Release();
            }

            context.UserContext.OnSend();

            context.Dispose();
        }
    }
}