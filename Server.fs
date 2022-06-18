module WebSocketer.Server

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Channels
open fsharper.typ
open fsharper.op.Alias
open WebSocketer.typ

/// 持续监听本机指定端口的tcp连接
/// 闭包 f 生命期结束后其连接会被自动销毁
/// 此函数会永久性阻塞当前线程
/// 该函的返回值始终为Error
let listen (port: u16) f =
    let listenSocket =
        new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

    let endPoint = IPEndPoint(IPAddress.Any, i32 port)

    listenSocket.Bind endPoint
    listenSocket.Listen 1024

    try
        while true do //持续阻塞当前线程
            let socket = listenSocket.Accept()

            async {
                try
                    let requestText = socket.recvAllUtf8 ()

                    if isHttpUpgradeWebsocket requestText then

                        requestText //send response
                        |> getSecWebSocketKey
                        |> genSecWebSocketAccept
                        |> genResponse
                        |> socket.sendUtf8

                        new WebSocket(socket) |> f
                with
                | e ->
                    e.Message |> Console.WriteLine
                    socket.Dispose()
            }
            |> Async.Start

            //监听连接状态，在断开时解除阻塞并开始新一轮监听
            async {
                let timeout = 500 //超时时间
                let span = 3000 //轮询间隔

                let disConnected () =
                    socket.Available = 0
                    && socket.Poll(timeout, SelectMode.SelectRead)

                while not (disConnected ()) do
                    Thread.Sleep span

                socket.Close()
            }
            |> Async.RunSynchronously

        Ok()

    with
    | e ->
        listenSocket.Dispose()
        Err e


/// 带有超时的版本
/// timeout是单位为毫秒的时间
/// 每次开始监听时都会进行计时，如果在timeout时间内没有连接产生，函数将返回
let listenWithTimeout (port: u16) f (timeout: u32) =
    if timeout > u32 Int32.MaxValue then //防止溢出
        failwith $"timeout is larger than {Int32.MaxValue}"

    let listenSocket =
        new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

    let endPoint = IPEndPoint(IPAddress.Any, i32 port)

    listenSocket.Bind endPoint
    listenSocket.Listen 1024

    try
        let rec loop () = //持续阻塞当前线程
            let e = new ManualResetEvent(false)
            let c = Channel.CreateUnbounded<IAsyncResult>()

            let callback =
                let f ar =
                    e.Set() |> ignore
                    c.Writer.TryWrite(ar) |> ignore

                AsyncCallback f

            listenSocket.BeginAccept(callback, null) |> ignore

            if e.WaitOne(i32 timeout, false) then
                let _, ar = c.Reader.TryRead()
                let socket = listenSocket.EndAccept(ar)

                async {
                    try
                        let requestText: string = socket.recvAllUtf8 ()

                        if isHttpUpgradeWebsocket requestText then

                            requestText //send response
                            |> getSecWebSocketKey
                            |> genSecWebSocketAccept
                            |> genResponse
                            |> socket.sendUtf8

                            new WebSocket(socket) |> f
                    with
                    | e ->
                        e.Message |> Console.WriteLine
                        socket.Dispose()

                }
                |> Async.Start

                //监听连接状态，在断开时解除阻塞并开始新一轮监听
                async {
                    let timeout = 500 //超时时间
                    let span = 3000 //轮询间隔

                    let disConnected () =
                        socket.Available = 0
                        && socket.Poll(timeout, SelectMode.SelectRead)

                    while not <| disConnected () do
                        Thread.Sleep span

                    socket.Close()
                }
                |> Async.RunSynchronously

                loop ()
            else
                ()

        loop () |> Ok
    with
    | e ->
        listenSocket.Dispose()
        Err e
