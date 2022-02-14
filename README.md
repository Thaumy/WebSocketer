# WebsSocketer

A simple websocket server for dotnet.

## Usage

Server (F#):

```F#
open WebSocketer
open WebSocketer.Server

let f (ws: WebSocket) =
    while true do
        let msg = ws.recv ()
        Console.WriteLine msg
        ws.send msg

    ()

//listen on localhost:20222, this will keep blocking the current thread 
listen 20222us f |> ignore 

(* 
Good Morning!
I'm Thaumy
Bye~
*)
```

Client (using Node.js for example):

```javascript
let WebSocketClient = require('websocket').client

let client = new WebSocketClient()

client.on('connectFailed', err => {
    console.log('Connect Err: ' + err.toString())
})

client.on('connect', conn => {
    console.log('WebSocket Client Connected')

    conn.on('error', err => {
        console.log("Connection Err: " + err.toString())
    })
    conn.on('close', () => {
        console.log('Connection Closed')
    })
    conn.on('message', msg => {
        console.log(`Received: '${msg.utf8Data.toString()}'`)
    })
    conn.send("Good Morning!")
    conn.send("I'm Thaumy")
    conn.send("Bye~")
})

client.connect('ws://localhost:20222/')

/* 
WebSocket Client Connected
Received: 'Good Morning!'
Received: 'I'm Thaumy'
Received: 'Bye~'
Connection Closed
*/
```

WIP.
