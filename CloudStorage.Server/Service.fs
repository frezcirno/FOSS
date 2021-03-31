module CloudStorage.Server.Service

open Giraffe
open Giraffe.ComputationExpressions
open CloudStorage.Server.Db

let CreateFileMeta (fileMeta: FileMetaEntity) =
    let row = tbl_file.Create()
    row.FileSha1 <- fileMeta.FileSha1
    row.FileName <- fileMeta.FileName
    row.FileSize <- Some fileMeta.FileSize
    row.FileLoc <- fileMeta.Location
    ctx.SubmitUpdates()


let UpdateFileMeta (fileMeta: FileMetaEntity) =
    let maybeFile =
        query {
            for row in tbl_file do
                where (row.FileSha1.Equals fileMeta.FileSha1)
                select (Some row)
                exactlyOneOrDefault
        }

    match maybeFile with
    | None -> ()
    | Some file ->
        file.FileName <- fileMeta.FileName
        file.FileSize <- Some fileMeta.FileSize
        file.FileLoc <- fileMeta.Location
        ctx.SubmitUpdates()



let DeleteFileMeta (fsha1: string) =
    let maybeFile =
        query {
            for row in tbl_file do
                where (row.FileSha1.Equals fsha1)
                select (Some row)
                headOrDefault
        }

    match maybeFile with
    | None -> ()
    | Some file ->
        file.Delete()
        ctx.SubmitUpdates()


let GetFileMetaByHash (fsha1: string) =
    opt {
        let! file =
            query {
                for row in tbl_file do
                    where (row.FileSha1.Equals fsha1)
                    select (Some row)
                    headOrDefault
            }

        return
            { FileMetaEntity.FileSha1 = file.FileSha1
              FileName = file.FileName
              FileSize = file.FileSize |> Option.defaultValue 0L
              Location = file.FileLoc
              UploadAt = file.CreateAt.ToIsoString() }
    }


let ListFileMeta () =
    query {
        for row in tbl_file do
            select (row)
    }
    |> Seq.map
        (fun row ->
            { FileMetaEntity.FileSha1 = row.FileSha1
              FileName = row.FileName
              FileSize = row.FileSize |> Option.defaultValue 0L
              Location = row.FileLoc
              UploadAt = row.CreateAt.ToIsoString() })
    |> Seq.toList


let GetLatestFileMetas count' =
    query {
        for row in tbl_file do
            sortBy (row.CreateAt)
            take count'
            select (row)
    }
    |> Seq.map
        (fun row ->
            { FileMetaEntity.FileSha1 = row.FileSha1
              FileName = row.FileName
              FileSize = row.FileSize |> Option.defaultValue 0L
              Location = row.FileLoc
              UploadAt = row.CreateAt.ToIsoString() })
    |> Seq.toList
