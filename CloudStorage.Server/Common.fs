namespace CloudStorage.Server

open System

[<AutoOpen>]
type FileMeta =
    { FileSha1: string
      FileName: string
      FileSize: int64
      Location: string
      UploadAt: string }

[<AutoOpen>]
type User =
    { Username: string
      Email: string
      Phone: string
      SignupAt: string
      LastActiveAt: string
      Status: int }
    
[<AutoOpen>]
type UserFile =
    { UserName: string
      FileHash: string
      FileName: string
      FileSize: Int64
      UploadAt: string
      LastUpdated: string }
