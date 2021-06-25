module CloudStorage.Server.Transporter

open System
open System.IO
open CloudStorage.Common
open RabbitMQ.Client.Events

let q =
    new RabbitMq.Queue(Config.Rabbit.TransQueueName)

let Transporter (ea: BasicDeliverEventArgs) =
    let msg = RabbitMsg.Parser.ParseFrom ea.Body.Span
    use stream = File.OpenRead msg.CurLocation
    MyOss.putObject msg.DstLocation stream

    if Database.File.UpdateFileLocByHash msg.FileHash msg.DstLocation then
        GC.Collect()
        GC.WaitForPendingFinalizers()
        File.Delete msg.CurLocation
    else
        q.Send ea.BasicProperties.ReplyTo msg

q.Consume Transporter |> ignore
