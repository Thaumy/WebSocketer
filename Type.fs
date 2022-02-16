[<AutoOpen>]
module WebSocketer.Type

open System
open System.Text
open System.Net.Sockets

type Socket with

    /// 发送文本消息
    member self.sendText: string -> unit =
        Encoding.UTF8.GetBytes >> self.Send >> ignore
    /// 发送字节消息
    member self.sendBytes: byte [] -> unit = self.Send >> ignore

    /// 接收文本消息
    member self.recvText() =
        let buf = Array.zeroCreate<byte> 4096

        let rec fetch (sb: StringBuilder) =
            match self.Receive buf with
            | n when n = buf.Length ->
                Encoding.UTF8.GetString(buf, 0, n)
                |> sb.Append
                |> fetch
            | n -> //缓冲区未满，说明全部接收完毕
                Encoding.UTF8.GetString(buf, 0, n) |> sb.Append

        (StringBuilder() |> fetch).ToString()
    /// 接收全部字节消息
    member self.recvBytes() =
        let buf = Array.zeroCreate<byte> 4096

        let rec fetch bl =
            match self.Receive buf with
            | readLen when readLen = buf.Length -> //尚未读完
                bl @ (buf |> Array.toList) |> fetch
            | readLen -> //缓冲区未满，说明全部接收完毕
                bl @ (buf.[0..readLen - 1] |> Array.toList)

        [] |> fetch |> List.toArray
    /// 接收指定长度字节消息
    member self.recvBytes(n) =
        let rec fetch buf start length =
            match self.Receive(buf, start, length, SocketFlags.None) with
            | readLen when readLen = length -> //读完
                buf
            | readLen -> fetch buf readLen (length - readLen)

        fetch (Array.zeroCreate<byte> n) 0 n

type WebSocket internal (socket: Socket) =
    member self.socket = socket

    member self.send(msg: string) =
        let msgBytes = Encoding.UTF8.GetBytes msg
        let actualPayLoadLen = msgBytes.Length
        //send FIN~OpCode
        socket.sendBytes [| 129uy |]

        if actualPayLoadLen < 126 then
            let payLoadLenByte = Convert.ToByte actualPayLoadLen

            //send MASK~PayLoadLen (MASK is 0)
            socket.sendBytes [| payLoadLenByte |]
            socket.sendBytes msgBytes
        elif actualPayLoadLen < 65536 then
            let payLoadLenByte = Convert.ToByte 126

            let actualPayLoadLenBytes =
                actualPayLoadLen
                |> Convert.ToUInt16
                |> BitConverter.GetBytes

            Array.Reverse(actualPayLoadLenBytes, 0, actualPayLoadLenBytes.Length)

            socket.sendBytes [| payLoadLenByte |]
            socket.sendBytes actualPayLoadLenBytes
            socket.sendBytes msgBytes
        else
            let payLoadLenByte = Convert.ToByte 127

            let actualPayLoadLenBytes =
                actualPayLoadLen
                |> Convert.ToUInt64
                |> BitConverter.GetBytes

            Array.Reverse(actualPayLoadLenBytes, 0, actualPayLoadLenBytes.Length)

            socket.sendBytes [| payLoadLenByte |]
            socket.sendBytes actualPayLoadLenBytes
            socket.sendBytes msgBytes

        ()

    member self.recv() =
        let payLoadLen =
            (socket.recvBytes 2).[1] &&& Byte.Parse("127")
            |> Convert.ToInt32

        if payLoadLen < 126 then
            let actualPayLoadLen = payLoadLen
            let maskBytes = (socket.recvBytes 4)
            let encodedBytes = (socket.recvBytes actualPayLoadLen)

            let decodedBytes =
                [| for i = 0 to actualPayLoadLen - 1 do
                       encodedBytes.[i] ^^^ maskBytes.[i % 4] |]

            decodedBytes |> Encoding.UTF8.GetString
        elif payLoadLen = 126 then
            //big endian to little endian
            let actualPayLoadLen =
                let actualPayLoadBytes = socket.recvBytes 2
                Array.Reverse(actualPayLoadBytes, 0, 2)

                actualPayLoadBytes
                |> BitConverter.ToUInt16
                |> Convert.ToInt32

            let maskBytes = (socket.recvBytes 4)
            let encodedBytes = (socket.recvBytes actualPayLoadLen)

            let decodedBytes =
                [| for i = 0 to actualPayLoadLen - 1 do
                       encodedBytes.[i] ^^^ maskBytes.[i % 4] |]

            decodedBytes |> Encoding.UTF8.GetString
        else //lengthBit eq 127
            let actualPayLoadLen =
                let actualPayLoadBytes = socket.recvBytes 8
                Array.Reverse(actualPayLoadBytes, 0, 8)

                actualPayLoadBytes
                |> BitConverter.ToUInt16
                |> Convert.ToInt32

            let maskBytes = (socket.recvBytes 4)
            let encodedBytes = (socket.recvBytes actualPayLoadLen)

            let decodedBytes =
                [| for i = 0 to actualPayLoadLen - 1 do
                       encodedBytes.[i] ^^^ maskBytes.[i % 4] |]

            decodedBytes |> Encoding.UTF8.GetString
