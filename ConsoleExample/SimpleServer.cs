using System;
using System.Collections.ObjectModel;
using System.Net;

using SimpleWebsockets.Server;
using SimpleWebsockets.Server.Classes;

namespace SimpleWebsockets.ConsoleExample
{
    class SimpleServer
    {
        private WebsocketServer _server;
        private Collection<UserContext> _clients;

        public void Start()
        {
            _clients = new Collection<UserContext>();

            _server = new WebsocketServer(81, IPAddress.Any)
            {
                DefaultOnReceive = OnReceive,
                DefaultOnSend = OnSend,
                DefaultOnConnect = OnConnect,
                DefaultOnDisconnect = OnDisconnect
            };

            _server.Start();
        }

        private void OnConnect(UserContext context)
        {
            _clients.Add(context);

            Console.WriteLine("Client connected: {0}", context.ClientAddress);
            Console.WriteLine("{0} clients are connected", _clients.Count);
        }

        private void OnDisconnect(UserContext context)
        {
            _clients.Remove(context);

            Console.WriteLine("Client disconnected: {0}", context.ClientAddress);
            Console.WriteLine("{0} clients are connected", _clients.Count);
        }

        private void OnSend(UserContext context)
        {
            Console.WriteLine("Sent '{0}' to {1}", 
                context.DataFrame.Payload, 
                context.ClientAddress);
        }

        private void OnReceive(UserContext context)
        {
            Console.WriteLine("Received '{0}' from {1}", 
                context.DataFrame.Payload, 
                context.ClientAddress);
        }
    }
}