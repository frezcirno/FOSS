module CloudStorage.Storage.Storage

open System.IO

let private StoragePath =
    "C:/dev/.net/CloudStorage/CloudStorage.Server/tmp/"

///
let putObjectAsync (key: string) (stream: Stream) =
    use newFile =
        File.Create(Path.Join [| StoragePath; key |])

    stream.CopyToAsync newFile

/// 获取
let getObject (key: string) : Stream =
    File.OpenRead(Path.Join [| StoragePath; key |]) :> Stream
