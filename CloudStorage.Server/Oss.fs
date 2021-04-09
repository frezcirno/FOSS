module CloudStorage.Server.Oss

open System.IO
open Aliyun.OSS
open Aliyun.OSS.Common;
open Aliyun.OSS.Util;

let private oss = OssClient(Config.OSSEndpoint, Config.OSSAccessKeyId, Config.OSSAccessKeySecret)

let putObject (key : string) (stream : Stream) =
    try
        let res = oss.PutObject(Config.OSSBucket, key, stream)
        Ok(res)
    with
    | :? OssException as ex ->
        printf "Failed with error code: %s; Error info: %s. \nRequestID:%s\tHostID:%s" ex.ErrorCode ex.Message ex.RequestId ex.HostId
        Error(ex)
