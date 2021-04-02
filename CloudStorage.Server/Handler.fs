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

let nothingHandler : HttpHandler = Util.apply

let WithToken (handlerNeedToken : (UserTokenBlock -> HttpHandler)) : HttpHandler =
    try bindModel None handlerNeedToken with
    | _ -> redirectTo false "/user/signin" 
           
let NeedToken : HttpHandler = WithToken (fun _ -> nothingHandler)

/// 用户上传文件
let FileUploadHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! user = ctx.BindFormAsync<UserTokenBlock> ()
            match ctx.Request.HasFormContentType with
            | false -> return! RequestErrors.BAD_REQUEST "Bad request" next ctx
            | true  ->
                for file in ctx.Request.Form.Files do
                    use fileStream = file.OpenReadStream()
                    let fileSha1 = Util.StreamSha1 fileStream
                    use newFile = File.Create ("C:/dev/.net/CloudStorage/CloudStorage.Server/tmp/" + fileSha1)
                    do! file.CopyToAsync newFile
                     
                    Database.CreateFileMeta fileSha1 file.FileName fileStream.Length newFile.Name |> ignore
                    Database.CreateUserFile user.username fileSha1 file.FileName |> ignore
                
                return! redirectTo false "/file/upload/suc" next ctx
        }

/// 用户文件元数据查询接口
let FileMetaHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        (match ctx.GetQueryStringValue "filehash" with
        | Error msg -> RequestErrors.BAD_REQUEST msg
        | Ok fileHash ->
            match Database.GetFileMetaByHash fileHash with
            | None ->
                json {
                    RespMsg.Code = -1
                    Msg = "no such file"
                    Data = ""
                }
            | Some fileMeta -> json fileMeta
        ) next ctx


let FileQueryHandler : HttpHandler =
    WithToken 
        (fun (userTokenBlock) (next : HttpFunc) (ctx : HttpContext) ->
            let limit = ctx.TryGetQueryStringValue "limit" |> Option.defaultValue "5" |> Int32.Parse
            let result = Database.GetLatestFileMetas limit  
            json {
                RespMsg.Code = 0
                Msg = "OK"
                Data = result
            } next ctx)
        
let UserFileQueryHandler (userTokenBlock) : HttpHandler =
    (fun (next : HttpFunc) (ctx : HttpContext) ->
        let limit = ctx.TryGetQueryStringValue "limit" |> Option.defaultValue "5" |> Int32.Parse
        let result = Database.GetLatestUserFileMetas userTokenBlock.username limit  
        json {
            RespMsg.Code = 0
            Msg = "OK"
            Data = result
        } next ctx)

/// 文件下载接口
let FileDownloadHandler : HttpHandler =
    bindModel None (fun user ->
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            return!
                (match ctx.GetQueryStringValue "filehash" with
                | Result.Error msg -> RequestErrors.BAD_REQUEST msg
                | Ok fileHash ->
                    match Database.IsUserHaveFile user.username fileHash with
                    | false -> RequestErrors.NOT_FOUND "File Not Found"
                    | true ->
                        match Database.GetFileMetaByHash fileHash with
                        | None -> RequestErrors.NOT_FOUND "File Not Found"
                        | Some fileMeta ->
                            streamFile true fileMeta.Location None None
                ) next ctx
        })

/// 文件更新接口
let FileUpdateHandler : HttpHandler =
    bindModel None (fun user ->
    tryBindForm RequestErrors.BAD_REQUEST None (fun fileBlock ->
    if fileBlock.op <> "O" then
        RequestErrors.METHOD_NOT_ALLOWED ""
    else 
        match Database.IsUserHaveFile user.username fileBlock.filehash with
        | false -> RequestErrors.NOT_FOUND "File Not Found"
        | true ->
            match Database.GetFileMetaByHash fileBlock.filehash with
            | None -> RequestErrors.NOT_FOUND "File Not Found"
            | Some (file : FileMeta) ->
                let newFile = { file with FileName = fileBlock.filename }
                Database.UpdateFileMeta newFile.FileSha1 newFile.FileName newFile.FileSize newFile.Location |> ignore
                Successful.ok (json newFile)))
        
/// 文件删除接口
let FileDeleteHandler : HttpHandler =
    bindModel None (fun user ->
    fun (next : HttpFunc) (ctx : HttpContext) ->
        (match ctx.GetQueryStringValue "filehash" with
        | Error msg -> RequestErrors.BAD_REQUEST msg
        | Ok fileHash ->
            match Database.IsUserHaveFile user.username fileHash with
            | false -> RequestErrors.NOT_FOUND "File Not Found"
            | true ->
            match Database.GetFileMetaByHash fileHash with
            | None -> Successful.OK "OK"
            | Some fileMeta ->
                File.Delete fileMeta.Location
                Database.DeleteFileMeta fileHash |> ignore
                Successful.OK "OK"
        ) next ctx)
        
/// 用户注册接口
let UserSignupHandler : HttpHandler =
    tryBindForm RequestErrors.BAD_REQUEST None (fun (signupBlock: SignupBlock) ->
        if signupBlock.username.Length < 3 || signupBlock.password.Length < 5 then
            RequestErrors.BAD_REQUEST "Invalid parameter"
        else
            let enc_password = Util.EncryptPasswd signupBlock.password
            match Database.UserSignup signupBlock.username enc_password with
            | false -> RequestErrors.BAD_REQUEST "Invalid parameter"
            | true -> Successful.OK "OK")

/// 用户登录接口
let UserSigninHandler : HttpHandler =
    tryBindForm RequestErrors.BAD_REQUEST None (fun (signinBlock: SigninBlock) ->
        fun (next : HttpFunc) (ctx : HttpContext) ->
            (let enc_password = Util.EncryptPasswd signinBlock.password
            match Database.UserSignin signinBlock.username enc_password with
            | false -> Successful.OK "FAILED"
            | true ->
                let token = Util.GenToken signinBlock.username
                match Database.UserUpdateToken signinBlock.username token with
                | false -> Successful.OK "FAILED"
                | true -> 
                    json {
                        RespMsg.Code = 0
                        Msg = "OK"
                        Data = 
                            {| Location = "http://" + ctx.Request.Host.Value + "/"
                               Username = signinBlock.username
                               Token = token |}
                    }
            ) next ctx)

/// 查询用户信息接口
let UserInfoHandler : HttpHandler =
    bindModel None (fun user ->
    match Database.GetUserByUsername user.username with
    | None -> id
    | Some user ->
        json {
            RespMsg.Code = 0
            Msg = "OK"
            Data = user
        })
