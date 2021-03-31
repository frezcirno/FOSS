namespace CloudStorage.Server

[<AutoOpen>]
type FileMetaEntity =
    { FileSha1 : string
      FileName : string
      FileSize : int64
      Location : string
      UploadAt : string }
    
     
    