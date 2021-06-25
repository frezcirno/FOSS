module CloudStorage.Common.MinioOss

open System
open System.IO
open Minio
open FSharp.Control.Tasks
open System.Threading.Tasks

let minio =
    MinioClient(Config.Minio.Endpoint, Config.Minio.AccessKey, Config.Minio.SecretKey)


let putObjectAsync (key: string) (stream: Stream) : Task<bool> =
    task {
        let size = stream.Length
        stream.Seek(0L, SeekOrigin.Begin) |> ignore
        do! minio.PutObjectAsync(Config.Minio.Bucket, key, stream, size)
        return! Task.FromResult true
    }


let putObject (key: string) (stream: Stream) = (putObjectAsync key stream).Wait()

let getObjectAsync (key: string) : Task<Stream> =
    task {
        let ms = new MemoryStream()
        do! minio.GetObjectAsync(Config.Minio.Bucket, key, (fun s -> s.CopyTo ms))
        ms.Seek(0L, SeekOrigin.Begin) |> ignore
        return upcast ms
    }

let getObject (key: string) : Stream = (getObjectAsync key).Result
