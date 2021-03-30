module CloudStorage.Server.Handlers

open System
open System.Globalization
open System.IO
open System.Security.Cryptography
open FSharp.Control.Tasks.V2.ContextInsensitive
open Microsoft.AspNetCore.Http
open Giraffe
open Giraffe.ComputationExpressions

open FileMeta

let ByteToHex (bytes:byte[]) =
    bytes |> Array.fold (fun state x -> state + sprintf "%02X" x) ""

let UploadHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.Request.HasFormContentType with
        | false -> RequestErrors.BAD_REQUEST "Bad request" next ctx
        | true  -> task {
            for file in ctx.Request.Form.Files do
                use fileStream = file.OpenReadStream()
                
                use sha1 = HashAlgorithm.Create ("sha1")
                let fileSha1 = fileStream |> sha1.ComputeHash |> ByteToHex
                
                use newFile = File.Create ("C:/dev/.net/CloudStorage/CloudStorage.Server/tmp/" + fileSha1)
                do! file.CopyToAsync (newFile)
                
                let fMeta =
                    { FileSha1 = fileSha1
                      FileName = file.FileName
                      FileSize = fileStream.Length
                      Location = newFile.Name
                      UploadAt = DateTime.Now.ToIsoString () }
                 
                UpdateFileMeta fMeta |> ignore
            return! redirectTo false "/file/upload/suc" next ctx
        }

let FileMetaHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        (match ctx.GetQueryStringValue "filehash" with
        | Error msg -> RequestErrors.BAD_REQUEST msg
        | Ok fileHash ->
            match GetFileMeta fileHash with
            | None -> Successful.NO_CONTENT
            | Some fMeta -> json fMeta
        ) next ctx

let FileQueryHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let limit = ctx.TryGetQueryStringValue "limit" |> Option.defaultValue "5" |> Int32.Parse
        json (GetLatestFileMetas limit) next ctx

let FileDownloadHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.GetQueryStringValue "filehash" with
        | Result.Error msg -> RequestErrors.BAD_REQUEST msg next ctx
        | Ok fileHash ->
            match GetFileMeta fileHash with
            | None -> RequestErrors.NOT_FOUND "File Not Found" next ctx
            | Some fMeta ->
                streamFile true fMeta.Location None None next ctx

[<CLIMutable>]
type FileMetaBlock = { filehash : string
                       filename: string
                       op: string }

let chinese = CultureInfo.CreateSpecificCulture("zh-CN")

let FileMetaUpdateHandler : HttpHandler =
    tryBindForm RequestErrors.BAD_REQUEST (Some chinese)
        (fun fileMetaBlock ->
            if fileMetaBlock.op <> "O" then
                RequestErrors.METHOD_NOT_ALLOWED ""
            else 
                match GetFileMeta fileMetaBlock.filehash with
                | None -> RequestErrors.NOT_FOUND "File Not Found"
                | Some fMeta ->
                    UpdateFileMeta {fMeta with FileName = fileMetaBlock.filename}
                    Successful.ok (json fMeta))

let FileDeleteHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        (match ctx.GetQueryStringValue "filehash" with
        | Error msg -> RequestErrors.BAD_REQUEST msg
        | Ok fileHash ->
            match GetFileMeta fileHash with
            | None -> Successful.OK "OK"
            | Some fMeta ->
                File.Delete fMeta.Location
                DeleteFileMeta fileHash |> ignore
                Successful.OK "OK"
        ) next ctx
        