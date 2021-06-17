open System
open System.IO
open CloudStorage.Common

let ProcessTransfer (msg: RabbitMsg) : bool =
    use stream = File.OpenRead msg.CurLocation
    MinioOss.putObject msg.DstLocation stream

    if Database.File.UpdateFileLocByHash msg.FileHash msg.DstLocation then
        GC.Collect()
        GC.WaitForPendingFinalizers()
        File.Delete msg.CurLocation
        true
    else
        false

[<EntryPoint>]
let main _ =
    RabbitMq.StartConsume Config.Rabbit.TransQueueName (Func<RabbitMsg, bool>(ProcessTransfer))
    printfn "Consumer started."
    0
