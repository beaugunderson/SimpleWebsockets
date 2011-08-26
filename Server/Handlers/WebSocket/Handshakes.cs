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
using System.Collections.Generic;
using System.Web;

using SimpleWebsockets.Server.Classes;

namespace SimpleWebsockets.Server.Handlers.WebSocket
{
    /// <summary>
    /// An easy wrapper for the header to access client handshake data.
    /// See http://www.whatwg.org/specs/web-socket-protocol/ for more details on the WebSocket Protocol.
    /// </summary>
    public class ClientHandshake
    {
        /// <summary>
        /// The preformatted handshake as a string.
        /// </summary>
        private const String Handshake = 
            "GET {0} HTTP/1.1\r\n" +
            "Upgrade: WebSocket\r\n" +
            "Connection: Upgrade\r\n" +
            "Origin: {1}\r\n" +
            "Host: {2}\r\n" +
            "Sec-Websocket-Key: {3}\r\n" +
            "{4}";

        public readonly string Origin = String.Empty;
        public readonly string Host = String.Empty;
        public readonly string ResourcePath = String.Empty;
        public readonly string Key = String.Empty;

        public HttpCookieCollection Cookies { get; set; }
        public string SubProtocol { get; set; }
        public Dictionary<string,string> AdditionalFields { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientHandshake"/> class.
        /// </summary>
        /// <param name="aHeader">The header.</param>
        public ClientHandshake(Header aHeader)
        {
            ResourcePath = aHeader.RequestPath;
            Key = aHeader["sec-websocket-key"];
            SubProtocol = aHeader["sec-websocket-protocol"];
            Origin = aHeader["sec-websocket-origin"];
            Host = aHeader["host"];
            Cookies = aHeader.Cookies;
        }

        /// <summary>
        /// Determines whether this instance is valid.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if this instance is valid; otherwise, <c>false</c>.
        /// </returns>
        public bool IsValid()
        {
            return (
                (Host != null) &&
                (Key != null) &&
                (Origin != null) &&
                (ResourcePath != null)
            );
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            string additionalFields = String.Empty;

            if (Cookies != null)
            {
                additionalFields += string.Format("Cookie: {0}\r\n", Cookies);
            }

            if (SubProtocol != null)
            {
                additionalFields += string.Format("Sec-Websocket-Protocol: {0}\r\n", SubProtocol);
            }

            if (AdditionalFields != null)
            {
                foreach (var field in AdditionalFields)
                {
                    additionalFields += string.Format("{0}: {1}\r\n", field.Key, field.Value);
                }
            }

            additionalFields += "\r\n";

            return String.Format(Handshake, ResourcePath, Origin, Host, Key, additionalFields);
        }
    }

    /// <summary>
    /// Implements a server handshake
    /// See http://www.whatwg.org/specs/web-socket-protocol/ for more details on the WebSocket Protocol.
    /// </summary>
    public class ServerHandshake
    {
        /// <summary>
        /// The preformatted handshake string.
        /// </summary>
        private const string Handshake = 
            "HTTP/1.1 101 Web Socket Protocol Handshake\r\n" +
                "Upgrade: WebSocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Origin: {0}\r\n" +
                "Sec-WebSocket-Location: {1}\r\n" +
                "Sec-WebSocket-Accept: {2}\r\n" +
                "{3}";

        public string Origin = String.Empty;
        public string Location = String.Empty;
        public string AnswerKey { get; set; }
        public string SubProtocol { get; set; }

        public Dictionary<string, string> AdditionalFields { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            string additionalFields = String.Empty;

            if (SubProtocol != null)
            {
                additionalFields += string.Format("Sec-WebSocket-Protocol: {0}\r\n", SubProtocol);
            }
            
            additionalFields += "\r\n";

            return String.Format(Handshake, Origin, Location, AnswerKey, additionalFields);
        }
    }
}
