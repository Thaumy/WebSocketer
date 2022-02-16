module WebSocketer.Server

open System
open System.Net
open System.Net.Sockets
open System.Threading

/// 持续监听本机指定端口的tcp连接
/// 闭包 f 生命期结束后其连接会被自动销毁
/// 此函数会永久性阻塞当前线程
let listen (port: uint16) f =
    let listenSocket =
        new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

    let endPoint = IPEndPoint(IPAddress.Any, int port)

    listenSocket.Bind endPoint
    listenSocket.Listen 1024

    try
        while true do //持续阻塞当前线程
            let socket = listenSocket.Accept()

            let sendHeartBeat =
                async {
                    let isConnected () =
                        (socket.Available = 0
                         && socket.Poll(1000, SelectMode.SelectRead))
                        |> not

                    while isConnected () do
                        Thread.Sleep(1000)

                        Console.Write "."

                    Console.Write "!"
                }


            async {
                try
                    let requestText: string = socket.recvText ()



                    if requestText.StartsWith("GET") then

                        let response =
                            requestText
                            |> getSecWebSocketKey
                            |> genSecWebSocketAccept
                            |> genResponse

                        socket.sendText response

                        socket |> WebSocket |> f
                with
                | e -> e.ToString() |> Console.WriteLine
            }
            |> Async.Start

            sendHeartBeat |> Async.RunSynchronously

            socket.Dispose()

        Ok()
    with
    | e ->
        listenSocket.Dispose()
        Error e
