namespace WebSocketer.typ

open System
open System.Net.Sockets
open fsharper.op.Alias
open fsharper.typ.Array
open WebSocketer

[<AutoOpen>]
module internal Socket_ext =

    type Socket with

        /// 发送字节数据
        member self.sendBytes(bytes: byte array) =
            match self.Send bytes with
            | sentLen when sentLen = bytes.Length -> () //全部发送完成
            | sentLen -> self.sendBytes bytes.[sentLen..^0] //继续发送剩余部分

        /// 接收指定长度字节数据
        member self.recvBytes(n: u32) =

            let rec fetch buf start remain =
                match self.Receive(buf, start, remain, SocketFlags.None) with
                | readLen when readLen = remain -> //读完
                    buf
                | readLen -> fetch buf readLen (remain - readLen)

            let n' = min (u32 Int32.MaxValue) n |> i32 //防止溢出

            fetch (Array.zeroCreate<byte> n') 0 n'

        /// 接收所有字节数据
        member self.recvAllBytes() =
            let buf = Array.zeroCreate<byte> 4096

            let rec fetch acc =
                match self.Receive buf with
                | readLen when readLen = buf.Length -> //尚未读完
                    acc @ buf.toList () |> fetch
                | readLen -> //缓冲区未满，说明全部接收完毕
                    acc @ buf.[0..readLen - 1].toList ()

            [] |> fetch |> List.toArray

    type Socket with

        /// 发送UTF8数据
        member self.sendUtf8(s: string) = s |> utf8ToBytes |> self.sendBytes

        /// 以UTF8编码接收所有数据
        member self.recvAllUtf8() = self.recvAllBytes () |> bytesToUtf8