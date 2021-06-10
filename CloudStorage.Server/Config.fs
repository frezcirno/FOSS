module CloudStorage.Server.Config

open System


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
    {| Endpoint = "localhost:9000"
       AccessKey = "admin"
       SecretKey = "password"
       Bucket = "test" |}

let Security =
    {| Secret = GetEnv "SECRET" "frezcirnoisthebest"
       Salt = GetEnv "SALT" "test"
       Tokensalt = GetEnv "TOKENSALT" "test" |}

let Data =
    {| datasource = "Server=localhost;Database=test;User=root;Password=root" |}

let Redis =
    "localhost:16379,password=root,abortConnect=false"

let Rabbit =
    {| AsyncTransferEnable = true
       RabbitURL = "amqp://guest:guest@127.0.0.1:5672/"
       TransExchangeName = "uploadserver.trans"
       TransOssQueueName = "uploadserver.trans.oss"
       TransOssErrQueueName = "uploadserver.trans.oss.err"
       TransOssRoutingKey = "oss" |}

let CHUNK_SIZE = 50 * 1024 * 1024
let TEMP_FILE_PATH = "tmp/"
