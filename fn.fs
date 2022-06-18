namespace WebSocketer

open System
open System.Text
open System.Text.RegularExpressions
open System.Security.Cryptography

//helpers

[<AutoOpen>]
module encoding =

    let bytesToUtf8 bytes =
        Encoding.UTF8.GetString(bytes, 0, bytes.Length)

    let utf8ToBytes (utf8: string) = Encoding.UTF8.GetBytes utf8
    
[<AutoOpen>]
module parser =
    
    let isHttpUpgradeWebsocket (requestText: string) =
        requestText.StartsWith("GET")
        && requestText.Contains("Upgrade: websocket")
    
    let getSecWebSocketKey requestText =
        (Regex("Sec-WebSocket-Key: (.*)").Match requestText)
            .Groups.[1]
            .Value.Trim()

    let genSecWebSocketAccept secWebSocketKey =
        $"{secWebSocketKey}258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
        |> utf8ToBytes
        |> SHA1.Create().ComputeHash
        |> Convert.ToBase64String

    let genResponse secWebSocketAccept =
        let EOL = "\r\n"

        $"HTTP/1.1 101 Switching Protocols{EOL}\
                     Connection: Upgrade{EOL}\
                        Upgrade: websocket{EOL}\
           Sec-WebSocket-Accept: {secWebSocketAccept}{EOL}{EOL}"
