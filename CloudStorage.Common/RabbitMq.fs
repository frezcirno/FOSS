module CloudStorage.Common.RabbitMq

open System
open System.Text
open System.Text.Json
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


/// 创建一个队列
/// s为空字符串是会随机新建一个队列名
type Queue(name: string) =
    let conn = fact.CreateConnection()
    let chan = conn.CreateModel()

    let queue =
        /// no-durable, no-exclusive, auto-delete
        chan.QueueDeclare(name, false, false, true, null)

    interface IDisposable with
        member this.Dispose() = this.Close()

    member _.Channel = chan
    member _.Name = queue.QueueName
    member _.Exchange = null

    /// Bind self to a exchange
    member this.Bind(exchange: string) =
        chan.QueueBind(this.Name, exchange, "", null)

    /// Send a message to another queue
    member this.Send (queue: string) (msg: obj) =
        let jsonString = JsonSerializer.Serialize(msg)
        let bytes = Encoding.UTF8.GetBytes jsonString
        let props = chan.CreateBasicProperties()
        props.ReplyTo <- this.Name
        chan.BasicPublish("", queue, props, ReadOnlyMemory(bytes))

    /// Publish a message, with my name
    /// Return customer tag
    member this.Publish (exchange: string) (msg: obj) =
        let jsonString = JsonSerializer.Serialize(msg)
        let bytes = Encoding.UTF8.GetBytes jsonString
        let props = chan.CreateBasicProperties()
        props.ReplyTo <- this.Name
        chan.BasicPublish(exchange, "", props, ReadOnlyMemory(bytes))

    member this.Consume(callback: BasicDeliverEventArgs -> unit) =
        let consumer = EventingBasicConsumer(chan)
        consumer.Received.Add callback
        chan.BasicConsume(this.Name, true, consumer)

    member this.BasicGet() = chan.BasicGet(this.Name, true)

    member this.Close() = chan.Close()


let conn = fact.CreateConnection()
let chan = conn.CreateModel()

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
