module WebSocketer.Server

open System.Net
open System.Net.Sockets

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
        while true do
            let socket = listenSocket.Accept()

            let requestText: string = socket.recv ()

            if requestText.StartsWith("GET") then

                let response =
                    requestText
                    |> getSecWebSocketKey
                    |> genSecWebSocketAccept
                    |> genResponse

                socket.send response

                socket |> WebSocket |> f

            socket.Dispose()

        Ok()
    with
    | e ->
        listenSocket.Dispose()
        Error e
