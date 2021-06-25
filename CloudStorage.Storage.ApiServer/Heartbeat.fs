module CloudStorage.Storage.ApiServer.Heartbeat

open System
open System.Text
open System.Threading
open CloudStorage.Common
open RabbitMQ.Client.Events


let mutex = obj ()
let dataServers = dict<string, DateTime> []

let removeExpiredDataServer () =
    while true do
        Thread.Sleep 5000
        Monitor.Enter mutex
        /// 10s无反应则删除
        for kv in dataServers do
            let t = kv.Value

            if t.Add(TimeSpan.FromSeconds 10.0) < DateTime.Now then
                /// is it ok?
                dataServers.Remove kv.Key |> ignore

        Monitor.Exit mutex


let ListenHeartbeat () =
    let q = new RabbitMq.Queue ""
    q.Bind "apiServers"

    let callback (msg: BasicDeliverEventArgs) =
        let dataServer = Encoding.UTF8.GetString msg.Body.Span
        Monitor.Enter mutex
        dataServers.Add(dataServer, DateTime.Now)
        Monitor.Exit mutex

    q.Consume callback |> ignore

    Thread(removeExpiredDataServer).Start()


let GetDataServers () : string list =
    Monitor.Enter mutex
    let res = dataServers.Keys |> Seq.toList
    Monitor.Exit mutex
    res

let ChooseRandomDataServer () : string =
    let res = GetDataServers()

    if res.IsEmpty then
        ""
    else
        Utils.choice res
