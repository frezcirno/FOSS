module CloudStorage.Server.RabbitMq

open System
open Google.Protobuf
open RabbitMQ.Client
open RabbitMQ.Client.Events

let private fact =
    ConnectionFactory(HostName = Config.Rabbit.RabbitURL)

let private conn = fact.CreateConnection()
let private chan = conn.CreateModel()

///
/// Create Exchange
///
chan.ExchangeDeclarePassive(Config.Rabbit.TransExchangeName)

///
/// Create Queue
///
chan.QueueDeclare(Config.Rabbit.TransOssQueueName, false, false, false, null)
|> ignore

///
/// Exchange ---(key)--> Queue
///
chan.QueueBind(Config.Rabbit.TransOssQueueName, Config.Rabbit.TransExchangeName, Config.Rabbit.TransOssRoutingKey)

let Publish (exchange: string) (routingKey: string) (msg: RabbitMsg) =
    let bytes = msg.ToByteArray()
    chan.BasicPublish(exchange, routingKey, false, null, ReadOnlyMemory<byte>(bytes))


///
/// Queue -> Consumer
///
let StartConsume (queue: string) (callback: Func<RabbitMsg, bool>) =
    let handler (ea: BasicDeliverEventArgs) =
        let msg = RabbitMsg.Parser.ParseFrom ea.Body.Span
        printfn $" [x] Received %s{ea.RoutingKey}"
        let res = callback.Invoke msg
        if not res then ()


    let customer = EventingBasicConsumer(chan)
    customer.Received.Add handler

    chan.BasicConsume(queue, true, customer) |> ignore
