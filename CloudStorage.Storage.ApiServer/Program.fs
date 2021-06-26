open CloudStorage.Storage.ApiServer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Giraffe

let Router =
    choose [ route "/ping" >=> Successful.OK "pong"
             routef "/object/%s" Objects.Handler
             routef "/locate/%s" Locate.Handler ]

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
    Heartbeat.ListenHeartbeat()

    WebHostBuilder()
        .UseConfiguration(
            ConfigurationBuilder()
                .AddCommandLine(args)
                .Build()
        )
        .UseKestrel(fun opt -> opt.AddServerHeader <- false)
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
