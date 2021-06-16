module CloudStorage.Common.RabbitMq

open System
open CloudStorage.Common
open CloudStorage.Common
open Google.Protobuf
open RabbitMQ.Client
open RabbitMQ.Client.Events

let private fact =
    ConnectionFactory(
        HostName = Config.Rabbit.HostName,
        UserName = Config.Rabbit.UserName,
        Password = Config.Rabbit.Password,
        Port = Config.Rabbit.Port,
        VirtualHost = Config.Rabbit.VirtualHost
    )

let private conn = fact.CreateConnection()
let private chan = conn.CreateModel()

///
/// Create Exchange
///
chan.ExchangeDeclare(Config.Rabbit.TransExchangeName, "direct")

///
/// Create Queues
///
chan.QueueDeclare(Config.Rabbit.TransQueueName, false, false, false, null)
|> ignore

chan.QueueDeclare(Config.Rabbit.TransErrQueueName, false, false, false, null)
|> ignore

///
/// Exchange ---(key)--> Queue
///
chan.QueueBind(Config.Rabbit.TransQueueName, Config.Rabbit.TransExchangeName, Config.Rabbit.TransRoutingKey)

chan.QueueBind(Config.Rabbit.TransErrQueueName, Config.Rabbit.TransExchangeName, Config.Rabbit.TransErrRoutingKey)

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

        if not res then
            Publish Config.Rabbit.TransExchangeName Config.Rabbit.TransErrRoutingKey msg


    let customer = EventingBasicConsumer(chan)
    customer.Received.Add handler

    chan.BasicConsume(queue, true, customer) |> ignore
