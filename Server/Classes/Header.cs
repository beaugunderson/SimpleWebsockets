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
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Web;

namespace SimpleWebsockets.Server.Classes
{
    /// <summary>
    /// What protocols we support
    /// </summary>
    public enum Protocol
    {
        None = -1,
        WebSocket = 0,
        FlashSocket = 1
    }

    /// <summary>
    /// This class implements a rudimentary HTTP header reading interface.
    /// </summary>
    public class Header
    {
        /// <summary>
        /// Regular expression to parse http header
        /// </summary>
        public const string Pattern = @"^(?<connect>[^\s]+)\s(?<path>[^\s]+)\sHTTP\/1\.1\r\n" + // HTTP Request
                                      @"((?<field_name>[^:\r\n]+):(?<field_value>[^\r\n]+)\r\n)+"; // HTTP Header Fields (<Field_Name>: <Field_Value> CR LF)

        /// <summary>
        /// The HTTP Method (GET/POST/PUT, etc.)
        /// </summary>
        public String Method = String.Empty;

        /// <summary>
        /// What protocol this header represents, if any.
        /// </summary>
        public Protocol Protocol = Protocol.None;

        /// <summary>
        /// The path requested by the header.
        /// </summary>
        public string RequestPath = string.Empty;

        /// <summary>
        /// Any cookies sent with the header.
        /// </summary>
        public readonly HttpCookieCollection Cookies = new HttpCookieCollection();

        /// <summary>
        /// A collection of fields attached to the header.
        /// </summary>
        private readonly NameValueCollection _fields = new NameValueCollection();

        /// <summary>
        /// Gets or sets the Fields object with the specified key.
        /// </summary>
        public string this[string Key]
        {
            get
            {
                return _fields[Key];
            }

            set
            {
                _fields[Key] = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Header"/> class.
        /// Accepts a string that represents an HTTP header.
        /// </summary>
        /// <param name="data">The data.</param>
        public Header(string data)
        {
            try
            {
                // Parse HTTP Header
                var regex = new Regex(Pattern, RegexOptions.IgnoreCase);
                var match = regex.Match(data);
                var someFields = match.Groups;

                // run through every match and save them in the handshake object
                for (int i = 0; i < someFields["field_name"].Captures.Count; i++)
                {
                    string name = someFields["field_name"].Captures[i].ToString().ToLower();
                    string value = someFields["field_value"].Captures[i].ToString().Trim();

                    switch (name)
                    {
                        case "cookie":
                            string[] CookieArray = value.Split(';');

                            foreach (var aCookie in CookieArray)
                            {
                                try
                                {
                                    string cookieName = aCookie.Remove(aCookie.IndexOf('='));
                                    string cookieValue = aCookie.Substring(aCookie.IndexOf('=') + 1);

                                    Cookies.Add(new HttpCookie(cookieName.TrimStart(), cookieValue));
                                }
                                catch { /* Ignore bad cookie */ }
                            }
                            
                            break;
                        default:
                            _fields.Add(name, value);
                            
                            break;
                    }
                }

                RequestPath = someFields["path"].Captures[0].Value.Trim();
                Method = someFields["connect"].Captures[0].Value.Trim();

                string[] pathExplode = RequestPath.Split('/');
                string protocolString = string.Empty;
                
                if (pathExplode.Length > 0)
                {
                    protocolString = pathExplode[pathExplode.Length - 1].ToLower().Trim();
                }

                switch (protocolString)
                {
                    case "websocket":
                        Protocol = Protocol.WebSocket;
                        
                        break;
                    case "flashsocket":
                        Protocol = Protocol.FlashSocket;
                        
                        break;
                    default:
                        Protocol = Protocol.None;
                        
                        break;
                }
            }
            catch{ /* Ignore bad header */ }
        }
    }
}
