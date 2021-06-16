open System
open System.IO
open CloudStorage.Common

let ProcessTransfer (msg: RabbitMsg) : bool =
    use stream = File.OpenRead msg.CurLocation
    MinioOss.putObject msg.DstLocation stream
    File.Delete msg.CurLocation
    Database.File.UpdateFileLocByHash msg.FileHash msg.DstLocation

[<EntryPoint>]
let main _ =
    RabbitMq.StartConsume Config.Rabbit.TransQueueName (Func<RabbitMsg, bool>(ProcessTransfer))
    printfn "Consumer started."
    0
