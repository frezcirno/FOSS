namespace CloudStorage.Server


[<AutoOpen>]
type FileMetaEntity =
    { FileSha1: string
      FileName: string
      FileSize: int64
      Location: string
      UploadAt: string }

[<AutoOpen>]
type UserEntity =
    { Username: string
      Email: string
      Phone: int64
      SignupAt: string
      LastActiveAt: string
      Status: int }
    
