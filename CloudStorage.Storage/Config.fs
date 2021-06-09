module CloudStorage.Storage.Config

open System

let private GetEnv = Environment.GetEnvironmentVariable

let Oss =
    {| Endpoint = GetEnv "OSSENDPOINT"
       AccessKeyId = GetEnv "OSSACCESSKEYID"
       AccessKeySecret = GetEnv "OSSACCESSKEYSECRET"
       Bucket = "fcirno-test" |}

let Redis =
    "localhost:6379,password=root,abortConnect=false"

let Data =
    {| datasource = "Server=localhost;Database=storage;User=root;Password=root" |}

let ZK_NODE_PREFIX = "/storage"

let STORAGE_PATH = "C:/tmp/"
