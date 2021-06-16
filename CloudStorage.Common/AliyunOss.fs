module CloudStorage.Common.AliyunOss

open System.IO
open Aliyun.OSS
open Aliyun.OSS.Common

let private oss =
    OssClient(Config.AliyunOss.Endpoint, Config.AliyunOss.AccessKeyId, Config.AliyunOss.AccessKeySecret)

let putObject (key: string) (stream: Stream) =
    try
        let res =
            oss.PutObject(Config.AliyunOss.Bucket, key, stream)

        Ok(res)
    with :? OssException as ex ->
        printf
            "Failed with error code: %s; Error info: %s. \nRequestID:%s\tHostID:%s"
            ex.ErrorCode
            ex.Message
            ex.RequestId
            ex.HostId

        Error(ex)

let getObject (key: string) : Stream =
    let res = oss.GetObject(Config.AliyunOss.Bucket, key)
    use stream = res.ResponseStream
    stream
