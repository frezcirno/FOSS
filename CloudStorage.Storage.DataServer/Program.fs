open System
open System.IO
open System.Text
open System.Threading
open CloudStorage.Common
open CloudStorage.Storage.DataServer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Giraffe
open RabbitMQ.Client.Events


let Router =
    choose [ route "/ping" >=> Successful.OK "pong"
             routef "/object/%s" Objects.Handler ]


let StartHeartbeat () =
    use q = new RabbitMq.Queue ""

    while true do
        let bindAddr =
            Environment.GetEnvironmentVariable "LISTEN_ADDRESS"

        q.Publish "apiServers" bindAddr
        Thread.Sleep 5000


let Locate (name: string) = File.Exists name


let StartLocate () =
    let q = new RabbitMq.Queue ""
    q.Bind "dataServers"

    let callback (msg: BasicDeliverEventArgs) =
        let objectName =
            (Encoding.UTF8.GetString msg.Body.Span).Trim('"')

        let path =
            Path.Join [| Config.TEMP_PATH
                         objectName |]

        /// 找到文件才回应
        if Locate path then
            let bindAddr =
                Environment.GetEnvironmentVariable "LISTEN_ADDRESS"

            q.Send msg.BasicProperties.ReplyTo bindAddr

    q.Consume callback


type Startup(configuration: IConfiguration) =
    member _.Configuration = configuration

    member _.ConfigureServices(services: IServiceCollection) =
        services.AddResponseCompression() |> ignore

        services.AddGiraffe() |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member _.Configure (app: IApplicationBuilder) (env: IHostEnvironment) (loggerFactory: ILoggerFactory) =
        env.EnvironmentName <- Environments.Development

        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage() |> ignore

        app.UseGiraffe Router


// 主程序
[<EntryPoint>]
let main args =
    Thread(StartHeartbeat).Start()

    StartLocate() |> ignore

    WebHostBuilder()
        .UseConfiguration(
            ConfigurationBuilder()
                .AddCommandLine(args)
                .Build()
        )
        .UseKestrel(fun opt ->
            opt.AddServerHeader <- false
            opt.Limits.MaxRequestBodySize <- 1024L * 1024L * 1024L)
        .UseStartup<Startup>()
        .ConfigureLogging(fun loggerBuilder ->
            loggerBuilder
                .AddFilter(fun lvl -> true)
                .AddConsole()
                .AddDebug()
            |> ignore)
        .Build()
        .Run()

    0
