module CloudStorage.Server.MinioOss

open System.IO
open Minio

let minio =
    MinioClient(Config.Minio.Endpoint, Config.Minio.AccessKey, Config.Minio.SecretKey)


let putObjectAsync (key: string) (stream: Stream) =
    let size = stream.Length
    stream.Seek(0L, SeekOrigin.Begin) |> ignore
    minio.PutObjectAsync(Config.Minio.Bucket, key, stream, size)


let getObjectAsync (key: string) =
    minio.GetObjectAsync(Config.Minio.Bucket, key, "file")
