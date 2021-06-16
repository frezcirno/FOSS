module CloudStorage.Common.MinioOss

open System
open System.IO
open Minio
open FSharp.Control.Tasks

let minio =
    MinioClient(Config.Minio.Endpoint, Config.Minio.AccessKey, Config.Minio.SecretKey)


let putObjectAsync (key: string) (stream: Stream) =
    let size = stream.Length
    stream.Seek(0L, SeekOrigin.Begin) |> ignore
    minio.PutObjectAsync(Config.Minio.Bucket, key, stream, size)

let putObject (key: string) (stream: Stream) = (putObjectAsync key stream).Wait()

let getObjectAsync (key: string) =
    task {
        let ms = new MemoryStream()
        do! minio.GetObjectAsync(Config.Minio.Bucket, key, (fun s -> s.CopyTo ms))
        ms.Seek(0L, SeekOrigin.Begin) |> ignore
        return ms
    }

let getObject (key: string) = (getObjectAsync key).Result
