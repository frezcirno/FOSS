open CloudStorage.Server
open System
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Giraffe

let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")

    clearResponse
    >=> ServerErrors.internalError (text ex.Message)


// 主程序
[<EntryPoint>]
let main argv =
    Dapper.FSharp.OptionTypes.register ()

    Zk.main ()

    Host
        .CreateDefaultBuilder(argv)
        .ConfigureAppConfiguration(fun config -> ())
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .UseStartup<Startup>()
                .UseKestrel(fun k -> k.AddServerHeader <- false)
            |> ignore)
        .ConfigureLogging(fun loggerBuilder ->
            loggerBuilder
                .AddFilter(fun lvl -> true)
                .AddConsole()
                .AddDebug()
            |> ignore)
        .Build()
        .Run()

    0
