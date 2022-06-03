[<AutoOpen>]
module internal WebSocketer.Util

open System.Text

let bytesToUtf8 bytes =
    Encoding.UTF8.GetString(bytes, 0, bytes.Length)

let utf8ToBytes (utf8: string) = Encoding.UTF8.GetBytes utf8
