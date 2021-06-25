module CloudStorage.Common.RabbitMq

open System
open System.Text
open System.Text.Json
open CloudStorage.Common
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

    /// Send a message to another queue (with my name)
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
