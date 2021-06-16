module CloudStorage.Common.Zmq

open NetMQ
open NetMQ.Sockets


let zmq_test () =
    use server =
        new ResponseSocket("@tcp://localhost:5556") // bind

    use client =
        new RequestSocket(">tcp://localhost:5556") // connect

    // Send a message from the client socket
    client.SendFrame("Hello")

    // Receive the message from the server socket
    let m1 = server.ReceiveFrameString()
    printfn $"From Client: %s{m1}"

    // Send a response back from the server
    server.SendFrame("Hi Back")

    // Receive the response from the client socket
    let m2 = client.ReceiveFrameString()
    printfn $"From Server: %s{m2}"
    exit 0