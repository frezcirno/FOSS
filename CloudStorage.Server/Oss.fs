module CloudStorage.Server.Oss

open System.IO
open Aliyun.OSS
open Aliyun.OSS.Common;

let private oss = OssClient(Config.Oss.Endpoint, Config.Oss.AccessKeyId, Config.Oss.AccessKeySecret)

let putObject (key : string) (stream : Stream) =
    try
        let res = oss.PutObject(Config.Oss.Bucket, key, stream)
        Ok(res)
    with
    | :? OssException as ex ->
        printf "Failed with error code: %s; Error info: %s. \nRequestID:%s\tHostID:%s" ex.ErrorCode ex.Message ex.RequestId ex.HostId
        Error(ex)

let getObject (key : string) : Stream =
    let res = oss.GetObject(Config.Oss.Bucket, key)
    use stream = res.ResponseStream
    stream
    
