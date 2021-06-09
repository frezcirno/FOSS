module CloudStorage.Storage.Api

open System
open System.IO
open System.Runtime.CompilerServices
open System.Text.Json
open CloudStorage.Common
open CloudStorage.Common.Utils
open CloudStorage.Storage.Redis
open CloudStorage.Storage.FileSystem
open FSharp.Control.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open Giraffe
open Newtonsoft.Json
open StackExchange.Redis

/// 系统底层文件对象存储接口，内部服务，无鉴权，类似文件系统
/// 对象存储：文件以uid识别，存储时不考虑文件元数据
/// 分布式，每台服务器都一样
/// 文件平衡：通过rabbitmq判断文件是否需要移动
/// 1. POST(hash,size) -> key 登记文件元数据
///  - 判断是否已经存在，在任意一台服务器上
/// 2. PUT(key,data) 上传文件内容
/// 3. HEAD(key) 判断文件是否存在
/// 3. GET(key) 获取文件内容
/// 4. DELETE(key) 删除文件

let ArgumentError (err: string) = RequestErrors.BAD_REQUEST err

[<CLIMutable>]
type PostFile = { size: UInt64; hash: string }

type UUID =
    { key: string /// 随机生成的id
      file_hash: string /// 文件hash
      file_size: uint64 } /// 文件大小

///
/// 登记一个上传任务，判断是否已存在，成功返回uid
///
let PostFileHandler (key: string) (next: HttpFunc) (ctx: HttpContext) =
    task {
        match! ctx.TryBindFormAsync<PostFile>() with
        | Error msg -> return! ArgumentError msg next ctx
        | Ok args ->
            /// TODO: 判断是否已存在

            let uuid =
                { key = key
                  file_hash = args.hash
                  file_size = args.size }

            let jsonUuid = JsonConvert.SerializeObject(uuid)

            let res =
                redis1.StringSet(RedisKey(key), RedisValue(jsonUuid), TimeSpan.FromMinutes(10.0))

            if not res then
                return! ServerErrors.serviceUnavailable id next ctx
            else
                return! Successful.OK key next ctx
    }

let PutFileHandler (key: string) (next: HttpFunc) (ctx: HttpContext) =
    task {
        use data = new MemoryStream()
        ctx.Request.Body.CopyTo data

        let rv =
            redis1.StringGet(RedisKey(key)).ToString()

        let uuid = JsonConvert.DeserializeObject<UUID> rv
        let hash = StreamSha1 data

        if not (uuid.file_hash.ToLower() = hash.ToLower()) then
            return! RequestErrors.FORBIDDEN "Hash unmatched" next ctx
        else

            /// 文件切片

            /// 文件备份

            /// 选择目标服务器
            ///
            data.Seek(0L, SeekOrigin.Begin) |> ignore
            do! PutObjectAsync key data
            return! Successful.ok id next ctx
    }

let GetFileHandler (key: string) (next: HttpFunc) (ctx: HttpContext) =
    if Exists key then
        let stream = GetObject key
        streamData true stream None None next ctx
    else
        RequestErrors.notFound id next ctx

let HeadFileHandler (key: string) (next: HttpFunc) (ctx: HttpContext) =
    if Exists key then
        Successful.ok id next ctx
    else
        RequestErrors.notFound id next ctx

let DeleteFileHandler (key: string) (next: HttpFunc) (ctx: HttpContext) =
    DeleteObject(key)
    Successful.ok id next ctx

let FileHandler (key: string) =
    choose [ PUT >=> PutFileHandler(key)
             POST >=> PostFileHandler(key)
             HEAD >=> HeadFileHandler(key)
             GET >=> GetFileHandler(key)
             DELETE >=> DeleteFileHandler(key) ]

let ErrorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")

    clearResponse
    >=> ServerErrors.INTERNAL_ERROR ex.Message


let Router =
    choose [ route "/ping" >=> Successful.OK "pong"
             routef "/file/%s" FileHandler ]


type Startup(configuration: IConfiguration) =
    member _.Configuration = configuration

    member this.ConfigureServices(services: IServiceCollection) =
        services.AddResponseCompression() |> ignore

        services.AddGiraffe() |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member _.Configure (app: IApplicationBuilder) (env: IHostEnvironment) (loggerFactory: ILoggerFactory) =
        env.EnvironmentName <- Environments.Development

        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage() |> ignore

        app
            .UseGiraffeErrorHandler(ErrorHandler)
            .UseGiraffe Router


// 主程序
[<EntryPoint>]
let main argv =
    Host
        .CreateDefaultBuilder(argv)
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .UseStartup<Startup>()
                .UseKestrel(fun opt -> opt.AddServerHeader <- false)
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
