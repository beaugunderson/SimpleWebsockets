using System;

namespace SimpleWebsockets.ConsoleExample
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new SimpleServer();

            server.Start();

            Console.ReadLine();
        }
    }
}