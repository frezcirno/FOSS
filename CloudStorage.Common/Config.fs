module CloudStorage.Common.Config

open System
open RabbitMQ.Client


let private GetEnv (key: string) (def: string) =
    let envs = Environment.GetEnvironmentVariables()

    if envs.Contains key then
        string envs.[key]
    else
        def

let AliyunOss =
    {| Endpoint = GetEnv "OSS_ENDPOINT" "test"
       AccessKeyId = GetEnv "OSS_ACCESS_KEY_ID" "test"
       AccessKeySecret = GetEnv "OSS_ACCESS_KEY_SECRET" "test"
       Bucket = "fcirno-test" |}

let Minio =
    {| Endpoint = "7c00h.xyz:9000"
       AccessKey = "frezcirno"
       SecretKey = "xxxxxxxxx"
       Bucket = "test" |}

let Security =
    {| Secret = GetEnv "SECRET" "frezcirnoisthebest"
       Salt = GetEnv "SALT" "test"
       Tokensalt = GetEnv "TOKENSALT" "test" |}

let Data =
    {| datasource = "Server=7c00h.xyz;Database=storage;User=frezcirno;Password=xxxxxxxxx" |}

let Redis =
    "7c00h.xyz:6379,password=frezcirnoisthebest,abortConnect=false"

let Rabbit =
    {| AsyncTransferEnable = true
       HostName = "7c00h.xyz"
       UserName = "frezcirno"
       Password = "xxxxxxxxx"
       Port = AmqpTcpEndpoint.UseDefaultPort
       VirtualHost = ConnectionFactory.DefaultVHost
       TransQueueName = "Transporter" |}

let Elastic = {| Server = "7c00h.xyz" |}

let MyOss = {| Server = [| "7c00h.xyz:8881"; "7c00h.xyz:8882"; "7c00h.xyz:8883"; |] |}

let CHUNK_SIZE = 5 * 1024 * 1024
let TEMP_FILE_PATH = "/tmp"

///
/// 每个分块文件：
/// HASH_KEY_PREFIX + hash -> uploadId
/// UPLOAD_INFO_KEY_PREFIX + uploadId -> {  }
/// CHUNK_KEY_PREFIX + uploadId -> [  ]
///
let HASH_KEY_PREFIX = "hash_"
let UPLOAD_INFO_KEY_PREFIX = "upid_"
let CHUNK_KEY_PREFIX = "ckid_"
