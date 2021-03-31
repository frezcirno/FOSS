module CloudStorage.Server.Handler

open System
open System.IO
open FSharp.Control.Tasks.V2.ContextInsensitive
open Microsoft.AspNetCore.Http
open Giraffe

open CloudStorage.Server


[<CLIMutable>]
type FileMetaBlock =
    { filehash : string
      filename: string
      op: string }


[<CLIMutable>]
type SignupBlock =
    { username: string
      password: string }
    
    
[<CLIMutable>]
type SigninBlock =
    { username: string
      password: string }
    
    
[<CLIMutable>]
type UserTokenBlock =
    { username: string
      token: string }
    
    
[<CLIMutable>]
type UserInfoBlock =
    { Username: string
      Email: string
      Phone: string
      SignupAt: string
      LastActiveAt: string
      Status: int }
    

type RespMsg<'a> = {
    Code: int
    Msg: string
    Data: 'a
}


let FileUploadHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.Request.HasFormContentType with
        | false -> RequestErrors.BAD_REQUEST "Bad request" next ctx
        | true  -> task {
            for file in ctx.Request.Form.Files do
                use fileStream = file.OpenReadStream()
                let fileSha1 = Util.StreamSha1 fileStream
                use newFile = File.Create ("C:/dev/.net/CloudStorage/CloudStorage.Server/tmp/" + fileSha1)
                do! file.CopyToAsync newFile
               
                let fileMeta =
                    { FileMetaEntity.FileSha1 = fileSha1
                      FileName = file.FileName
                      FileSize = fileStream.Length
                      Location = newFile.Name
                      UploadAt = DateTime.Now.ToIsoString() }
                 
                Database.CreateFileMeta fileMeta |> ignore
            return! redirectTo false "/file/upload/suc" next ctx
        }

let FileMetaHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        (match ctx.GetQueryStringValue "filehash" with
        | Error msg -> RequestErrors.BAD_REQUEST msg
        | Ok fileHash ->
            match Database.GetFileMetaByHash fileHash with
            | None -> Successful.NO_CONTENT
            | Some fileMeta -> json fileMeta
        ) next ctx

let FileQueryHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let limit = ctx.TryGetQueryStringValue "limit" |> Option.defaultValue "5" |> Int32.Parse
        json (Database.GetLatestFileMetas limit) next ctx

let FileDownloadHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.GetQueryStringValue "filehash" with
        | Result.Error msg -> RequestErrors.BAD_REQUEST msg next ctx
        | Ok fileHash ->
            match Database.GetFileMetaByHash fileHash with
            | None -> RequestErrors.NOT_FOUND "File Not Found" next ctx
            | Some fileMeta ->
                streamFile true fileMeta.file_loc None None next ctx

let FileUpdateHandler : HttpHandler =
    tryBindForm RequestErrors.BAD_REQUEST None (fun fileMetaBlock ->
        if fileMetaBlock.op <> "O" then
            RequestErrors.METHOD_NOT_ALLOWED ""
        else 
            match Database.GetFileMetaByHash fileMetaBlock.filehash with
            | None -> RequestErrors.NOT_FOUND "File Not Found"
            | Some x ->
                let fileMetaObj = {
                        FileMetaEntity.FileSha1 = x.file_sha1
                        FileName = fileMetaBlock.filename
                        FileSize = x.file_size
                        Location = x.file_loc
                        UploadAt = x.create_at.ToIsoString() }
                Database.UpdateFileMeta fileMetaObj
                Successful.ok (json fileMetaObj))

let FileDeleteHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        (match ctx.GetQueryStringValue "filehash" with
        | Error msg -> RequestErrors.BAD_REQUEST msg
        | Ok fileHash ->
            match Database.GetFileMetaByHash fileHash with
            | None -> Successful.OK "OK"
            | Some fileMeta ->
                File.Delete fileMeta.file_loc
                Database.DeleteFileMeta fileHash |> ignore
                Successful.OK "OK"
        ) next ctx
        
let UserSignupHandler : HttpHandler =
    tryBindForm RequestErrors.BAD_REQUEST None (fun (signupBlock: SignupBlock) ->
        if signupBlock.username.Length < 3 || signupBlock.password.Length < 5 then
            RequestErrors.BAD_REQUEST "Invalid parameter"
        else
            let enc_password = Util.EncryptPasswd signupBlock.password
            match Database.UserSignup signupBlock.username enc_password with
            | 0 -> RequestErrors.BAD_REQUEST "Invalid parameter"
            | _ -> Successful.OK "OK")


let UserSigninHandler : HttpHandler =
    tryBindForm RequestErrors.BAD_REQUEST None (fun (signinBlock: SigninBlock) ->
        fun (next : HttpFunc) (ctx : HttpContext) ->
            (let enc_password = Util.EncryptPasswd signinBlock.password
            match Database.UserSignin signinBlock.username enc_password with
            | false -> Successful.OK "FAILED"
            | true ->   
                let token = Util.GenToken signinBlock.username
                match Database.UserUpdateToken signinBlock.username token with
                | 0 -> Successful.OK "FAILED"
                | _ -> 
                    json {
                        RespMsg.Code = 0
                        Msg = "OK"
                        Data = 
                            {| Location = "http://" + ctx.Request.Host.Value + "/"
                               Username = signinBlock.username
                               Token = token |}
                    }
            ) next ctx)


let UserInfoHandler : HttpHandler =
    bindForm None (fun (userTokenBlock: UserTokenBlock) ->
        match Database.GetUserByUsername userTokenBlock.username with
        | None -> id
        | Some userInfoObj ->
            json {
                RespMsg.Code = 0
                Msg = "OK"
                Data = {
                    UserInfoBlock.Username = userInfoObj.user_name
                    Email = userInfoObj.email
                    Phone = userInfoObj.phone
                    SignupAt = userInfoObj.signup_at.ToIsoString()
                    LastActiveAt = userInfoObj.last_active.ToIsoString()
                    Status = userInfoObj.status
                }
            }
    )
        
let CheckToken : HttpHandler =
    tryBindForm
        (fun msg -> RequestErrors.FORBIDDEN "Please Sign In first!")
        None
        (fun (userTokenBlock: UserTokenBlock) ->
            match Util.IsTokenValid userTokenBlock.username userTokenBlock.token with
            | true -> id
            | false -> RequestErrors.FORBIDDEN "Please Sign In first!"
        )
