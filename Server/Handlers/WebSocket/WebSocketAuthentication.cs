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
using System.Security.Cryptography;

using SimpleWebsockets.Server.Classes;

namespace SimpleWebsockets.Server.Handlers.WebSocket
{
    /// <summary>
    /// Handles the handshaking between the client and the host, when a new connection is created
    /// </summary>
    static class WebSocketAuthentication
    {
        public static string Origin = string.Empty;
        public static string Location = string.Empty;

        public static bool CheckHandshake(Context context)
        {
            if(context.ReceivedByteCount > 8)
            {
                var AHandshake = new ClientHandshake(context.Header);

                // See if our header had the required information
                if (AHandshake.IsValid())
                {
                    // Optionally check Origin and Location if they're set.
                    if (Origin != string.Empty &&
                        AHandshake.Origin != "http://" + Origin)
                    {
                        return false;
                    }
                    
                    if (Location != string.Empty &&
                        AHandshake.Host != string.Format("{0}:{1}", Location, context.Server.Port))
                    {
                        return false;
                    }

                    // Generate response handshake for the client
                    var serverShake = GenerateResponseHandshake(AHandshake);
                    
                    // Send the response handshake
                    SendServerHandshake(serverShake, context);
                    
                    return true;
                }
            }
            return false;
        }

        private static ServerHandshake GenerateResponseHandshake(ClientHandshake aHandshake)
        {
            var aResponseHandshake = new ServerHandshake
            {
                Location = string.Format("ws://{0}{1}", aHandshake.Host, aHandshake.ResourcePath),
                Origin = aHandshake.Origin,
                SubProtocol = aHandshake.SubProtocol,
                AnswerKey = GenerateAnswerKey(aHandshake.Key)
            };

            return aResponseHandshake;
        }
        
        private static void SendServerHandshake(ServerHandshake aHandshake, Context context)
        {
            // generate a byte array representation of the handshake including the answer to the challenge
            byte[] handshakeBytes = context.UserContext.Encoding.GetBytes(aHandshake.ToString());

            context.UserContext.SendRaw(handshakeBytes);
        }

        private static string GenerateAnswerKey(string key)
        {
            string concatenated = key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

            var bytes = Encoding.UTF8.GetBytes(concatenated);

            var hash = SHA1.Create().ComputeHash(bytes);

            return Convert.ToBase64String(hash);
        }
    }
}