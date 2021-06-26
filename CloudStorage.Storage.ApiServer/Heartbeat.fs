module CloudStorage.Storage.ApiServer.Heartbeat

open System
open System.Collections.Generic
open System.Text
open System.Threading
open CloudStorage.Common
open RabbitMQ.Client.Events


let mutex = obj ()
let dataServers = Dictionary<string, DateTime>()

let removeExpiredDataServer () =
    while true do
        Thread.Sleep 5000
        Monitor.Enter mutex
        /// 10s无反应则删除
        for kv in dataServers do
            let t = kv.Value

            if t.Add(TimeSpan.FromSeconds 30.0) < DateTime.Now then
                /// is it ok?
                System.Diagnostics.Debug.WriteLine(sprintf "%s no heartbeat, drop it." kv.Key)
                dataServers.Remove kv.Key |> ignore

        Monitor.Exit mutex


let ListenHeartbeat () =
    let q = new RabbitMq.Queue ""
    q.Bind "apiServers"

    let callback (ea: BasicDeliverEventArgs) =
        let str = Encoding.UTF8.GetString ea.Body.Span
        let dataServer = str.Trim('"')

        System.Diagnostics.Debug.WriteLine(sprintf "%s comes in." dataServer)

        try
            try
                Monitor.Enter mutex
                dataServers.[dataServer] <- DateTime.Now
            with ex ->
                ex
                ()
        finally
            Monitor.Exit mutex

    q.Consume callback |> ignore

    Thread(removeExpiredDataServer).Start()


let GetDataServers () : string list =
    try
        try
            Monitor.Enter mutex
            dataServers.Keys |> Seq.toList
        with ex ->
            ex
            []
    finally
        Monitor.Exit mutex

let ChooseRandomDataServer () : string =
    let res = GetDataServers()

    if res.IsEmpty then
        ""
    else
        Utils.choice res
