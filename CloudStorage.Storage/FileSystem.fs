module CloudStorage.Storage.FileSystem

open System.IO
open Config

if not (Directory.Exists STORAGE_PATH) then
    Directory.CreateDirectory STORAGE_PATH |> ignore

/// 放置文件
let PutObjectAsync (key: string) (stream: Stream) =
    use newFile =
        File.Create(Path.Join [| STORAGE_PATH; key |])

    stream.CopyToAsync newFile

/// 获取文件
let GetObject (key: string) : Stream =
    File.OpenRead(Path.Join [| STORAGE_PATH; key |]) :> Stream

let Exists (key: string) : bool =
    File.Exists(Path.Join [| STORAGE_PATH; key |])

/// 获取文件
let GetObjectMeta (key: string) =
    File.GetAttributes(Path.Join [| STORAGE_PATH; key |])

/// 删除文件
let DeleteObject (key: string) =
    File.Delete(Path.Join [| STORAGE_PATH; key |])
