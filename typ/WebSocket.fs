namespace WebSocketer.typ

open System
open System.IO
open System.Net.Sockets
open fsharper.op.Alias
open fsharper.typ.Array
open WebSocketer

type WebSocket internal (sendBytes, sendUtf8, recvBytes, recvAllUtf8, dispose) as ws =

    do
        let requestText = recvAllUtf8 ()

        //TODO 满足报文长度后再读取，例如： listenSocket.Available>36
        if isHttpUpgradeWebsocket requestText then

            requestText //send response
            |> getSecWebSocketKey
            |> genSecWebSocketAccept
            |> genResponse
            |> sendUtf8

    new(client: Socket) =
        new WebSocket(client.sendBytes, client.sendUtf8, client.recvBytes, client.recvAllUtf8, client.Dispose)

    new(client: TcpClient) =
        let ns = client.GetStream()

        new WebSocket(
            ns.sendBytes,
            ns.sendUtf8,
            ns.recvBytes,
            ns.recvAllUtf8,
            client.Dispose //TODO is this will dispose ns?
        )

    member self.send(msg: string) =
        let msgBytes = utf8ToBytes msg
        let actualPayLoadLen = msgBytes.Length

        let ms = new MemoryStream()

        //send FIN~OpCode
        ms.Write [| 129uy |]

        if actualPayLoadLen < 126 then
            let payLoadLenByte = [| Convert.ToByte actualPayLoadLen |]

            //send MASK~PayLoadLen (MASK is 0)
            ms.Write payLoadLenByte

        elif actualPayLoadLen < 65536 then
            let payLoadLenByte = [| 126uy |]

            let actualPayLoadLenBytes =
                actualPayLoadLen
                |> Convert.ToUInt16
                |> BitConverter.GetBytes

            reverseArray actualPayLoadLenBytes

            ms.Write payLoadLenByte
            ms.Write actualPayLoadLenBytes
        else
            let payLoadLenByte = [| 127uy |]

            let actualPayLoadLenBytes =
                actualPayLoadLen
                |> Convert.ToUInt64
                |> BitConverter.GetBytes

            reverseArray actualPayLoadLenBytes

            ms.Write payLoadLenByte
            ms.Write actualPayLoadLenBytes

        ms.Write msgBytes
        ms.ToArray() |> sendBytes


    member self.recv() =
        let payLoadLen = (recvBytes 2u).[1] &&& 127uy

        let actualPayLoadLen =
            if payLoadLen < 126uy then
                u32 payLoadLen
            elif payLoadLen = 126uy then

                let actualPayLoadBytes = recvBytes 2u

                reverseArray actualPayLoadBytes //big endian to little endian

                actualPayLoadBytes
                |> BitConverter.ToUInt16
                |> Convert.ToUInt32
            else //lengthBit is 127
                let actualPayLoadBytes = recvBytes 8u

                reverseArray actualPayLoadBytes

                actualPayLoadBytes
                |> BitConverter.ToUInt64
                |> Convert.ToUInt32

        let maskBytes = recvBytes 4u
        let encodedBytes = recvBytes actualPayLoadLen

        let decodedBytes =
            [| for i = 0 to i32 (actualPayLoadLen - 1u) do
                   encodedBytes.[i] ^^^ maskBytes.[i % 4] |]

        bytesToUtf8 decodedBytes

    member self.Dispose() = dispose ()

    interface IDisposable with
        member i.Dispose() = ws.Dispose()
