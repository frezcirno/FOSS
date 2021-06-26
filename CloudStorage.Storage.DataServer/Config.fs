module CloudStorage.Storage.DataServer.Config

open System.IO

let TEMP_PATH = "/tmp"

Directory.CreateDirectory TEMP_PATH |> ignore
