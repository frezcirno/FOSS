open CloudStorage.Server
open System
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Giraffe

let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")

    clearResponse
    >=> setStatusCode 500
    >=> text ex.Message

let ConfigureLogging (loggerBuilder: ILoggingBuilder) =
    loggerBuilder
        .AddFilter(fun lvl -> true)
        .AddConsole()
        .AddDebug()
    |> ignore


// 主程序
[<EntryPoint>]
let main argv =
    Host
        .CreateDefaultBuilder(argv)
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .UseStartup<Startup>()
                .UseKestrel(fun k -> k.AddServerHeader <- false)
            |> ignore)
        .ConfigureLogging(ConfigureLogging)
        .Build()
        .Run()

    0
