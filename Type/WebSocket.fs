[<AutoOpen>]
module WebSocketer.Type.WebSocket

open System
open System.IO
open System.Net
open System.Net.Sockets
open fsharper.types.Procedure
open WebSocketer
open WebSocketer.Parser
open WebSocketer.Type.Socket


type WebSocket internal (socket: Socket) =
    /// RAII构造
    new(port: uint16) =
        let listenSocket =
            new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

        let ep = IPEndPoint(IPAddress.Any, int port)

        listenSocket.Bind ep
        listenSocket.Listen 1024

        let rec fetchSocket () =
            let socket = listenSocket.Accept()
            let requestText = socket.recvAllUtf8 ()

            if isHttpUpgradeWebsocket requestText then

                requestText //send response
                |> getSecWebSocketKey
                |> genSecWebSocketAccept
                |> genResponse
                |> socket.sendUtf8

                socket
            else
                fetchSocket ()

        listenSocket.Dispose()

        new WebSocket(fetchSocket ())

    //对于使用其他API，暴露内部socket可能会有用
    member self.socket = socket

    member self.send(msg: string) =
        let msgBytes = utf8ToBytes msg
        let actualPayLoadLen = msgBytes.Length

        let stream = new MemoryStream()

        //send FIN~OpCode
        stream.Write [| 129uy |]

        if actualPayLoadLen < 126 then
            let payLoadLenByte = [| Convert.ToByte actualPayLoadLen |]

            //send MASK~PayLoadLen (MASK is 0)
            stream.Write payLoadLenByte

        elif actualPayLoadLen < 65536 then
            let payLoadLenByte = [| 126uy |]

            let actualPayLoadLenBytes =
                actualPayLoadLen
                |> Convert.ToUInt16
                |> BitConverter.GetBytes

            reverseArray actualPayLoadLenBytes

            stream.Write payLoadLenByte
            stream.Write actualPayLoadLenBytes
        else
            let payLoadLenByte = [| 127uy |]

            let actualPayLoadLenBytes =
                actualPayLoadLen
                |> Convert.ToUInt64
                |> BitConverter.GetBytes

            reverseArray actualPayLoadLenBytes

            stream.Write payLoadLenByte
            stream.Write actualPayLoadLenBytes

        stream.Write msgBytes
        stream.ToArray() |> socket.sendBytes

    member self.recv() =
        let payLoadLen = (socket.recvBytes 2u).[1] &&& 127uy

        let actualPayLoadLen =
            if payLoadLen < 126uy then
                uint32 payLoadLen
            elif payLoadLen = 126uy then

                let actualPayLoadBytes = socket.recvBytes 2u

                reverseArray actualPayLoadBytes //big endian to little endian

                actualPayLoadBytes
                |> BitConverter.ToUInt16
                |> Convert.ToUInt32
            else //lengthBit is 127
                let actualPayLoadBytes = socket.recvBytes 8u

                reverseArray actualPayLoadBytes

                actualPayLoadBytes
                |> BitConverter.ToUInt64
                |> Convert.ToUInt32

        let maskBytes = socket.recvBytes 4u
        let encodedBytes = socket.recvBytes actualPayLoadLen

        let decodedBytes =
            [| for i = 0 to int (actualPayLoadLen - 1u) do
                   encodedBytes.[i] ^^^ maskBytes.[i % 4] |]

        bytesToUtf8 decodedBytes

    interface IDisposable with
        member self.Dispose() = socket.Dispose()

    //destructor
    override self.Finalize() = (self :> IDisposable).Dispose()
