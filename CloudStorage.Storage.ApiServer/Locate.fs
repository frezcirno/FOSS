module CloudStorage.Storage.ApiServer.Locate

open System.Text
open System.Threading
open CloudStorage.Common
open Giraffe


/// 查询dataServer，找不到返回null
let Locate (name: string) =
    use q = new RabbitMq.Queue ""
    q.Publish "dataServers" name

    /// Waiting for 1 second(s)
    Thread.Sleep 1000
    let maybeMsg = q.BasicGet()

    if maybeMsg = null then
        ""
    else
        (Encoding.UTF8.GetString maybeMsg.Body.Span)
            .Trim('"')

let Exist (name: string) : bool = Locate(name) <> null

let LocateHandler (objectName: string) =
    let info = Locate objectName

    if info = null then
        RequestErrors.notFound id
    else
        json info

let Handler (objectName: string) =
    choose [ GET >=> LocateHandler objectName ]
