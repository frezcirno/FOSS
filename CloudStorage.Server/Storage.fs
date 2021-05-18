module CloudStorage.Server.Storage

open System.IO
open FSharp.Control.Tasks.V2.ContextInsensitive

let private StoragePath =
    "C:/dev/.net/CloudStorage/CloudStorage.Server/tmp/"

/// 
let putObject (key: string) (stream: Stream) =
    task {
        use newFile =
            File.Create(Path.Join [| StoragePath; key |])

        do! stream.CopyToAsync newFile
    }

/// 获取
let getObject (key: string) : Stream =
    File.OpenRead(Path.Join [| StoragePath; key |]) :> Stream


let putObjectBytes (key: string) (bytes: byte []) =
    task {
        let filePath = Path.Join [| StoragePath; key |]
        use newFile = File.Create(filePath)
        newFile.Write(bytes, 0, bytes.Length)
        return newFile
    }
