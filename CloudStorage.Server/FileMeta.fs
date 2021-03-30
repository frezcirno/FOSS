module CloudStorage.Server.FileMeta

open System.Collections.Generic

type FileMeta =
    { FileSha1 : string
      FileName : string
      FileSize : int64
      Location : string
      UploadAt : string }
    
let fileMetas = Dictionary<string, FileMeta>[]
     
let UpdateFileMeta (fMeta : FileMeta) =
    if fileMetas.ContainsKey fMeta.FileSha1 then
        fileMetas.[fMeta.FileSha1] <- fMeta
    else
        fileMetas.Add (fMeta.FileSha1, fMeta)
        
let GetFileMeta (fsha1 : string) =
    match fileMetas.ContainsKey fsha1 with
    | true -> Some fileMetas.[fsha1]
    | false -> None
    
let GetLatestFileMetas count =
    fileMetas.Values
    |> Seq.sortByDescending (fun meta -> meta.UploadAt)
    |> Seq.take (min fileMetas.Values.Count count)
    |> Seq.toList
    
let DeleteFileMeta (fsha1 : string) =
    fileMetas.Remove fsha1
    
    