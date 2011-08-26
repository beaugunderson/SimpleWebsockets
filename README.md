SimpleWebsockets
================

A websocket server built in C# with a focus on simplicity.

Forked from Olivine Labs' [Alchemy Websockets](https://github.com/Olivine-Labs/Alchemy-Websockets) project.

Usage
-----

```c#
var server = new WebsocketServer(81, IPAddress.Any)
{
    DefaultOnReceive = OnReceive,
    DefaultOnSend = OnSend,
    DefaultOnConnect = OnConnect,
    DefaultOnDisconnect = OnDisconnect,
    TimeOut = new TimeSpan(0, 5, 0)
};

server.Start();
```
