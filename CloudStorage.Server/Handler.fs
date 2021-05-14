module CloudStorage.Server.Handler

open System
open System.IO
open System.Security.Claims
open FSharp.Control.Tasks.V2.ContextInsensitive
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Http
open Giraffe
open StackExchange.Redis
open CloudStorage.Server
open CloudStorage.Server.Oss
open Microsoft.AspNetCore.Authentication

[<CLIMutable>]
type FileMetaBlock =
    { filehash: string
      filename: string
      op: string }


[<CLIMutable>]
type SignupBlock = { username: string; password: string }


[<CLIMutable>]
type SigninBlock = { username: string; password: string }


[<CLIMutable>]
type UserTokenBlock = { username: string; token: string }


[<CLIMutable>]
type UserInfoBlock =
    { Username: string
      Email: string
      Phone: string
      SignupAt: string
      LastActiveAt: string
      Status: int }


type RespMsg<'a> = { Code: int; Msg: string; Data: 'a }

let jsonResp code msg data =
    json
        { RespMsg.Code = code
          Msg = msg
          Data = data }

let WithToken (innerHandler: UserTokenBlock -> HttpHandler) : HttpHandler =
    tryBindQuery
        (fun err ->
            printfn $"%s{err}"
            redirectTo false "/user/signin")
        None
        innerHandler

let notLoggedIn : HttpHandler =
    RequestErrors.UNAUTHORIZED "Cookie" "SAFE Realm" "You must be logged in."

let jwtAuthorized : HttpHandler =
    requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme)


let cookieAuthorized : HttpHandler =
    requiresAuthentication (challenge CookieAuthenticationDefaults.AuthenticationScheme)



/// 用户上传文件
let FileUploadHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let! user = ctx.BindFormAsync<UserTokenBlock>()

            match ctx.Request.HasFormContentType with
            | false -> return! RequestErrors.BAD_REQUEST "Bad request" next ctx
            | true ->
                for file in ctx.Request.Form.Files do
                    use fileStream = file.OpenReadStream()
                    let fileSha1 = Util.StreamSha1 fileStream

                    use newFile =
                        File.Create(
                            "C:/dev/.net/CloudStorage/CloudStorage.Server/tmp/"
                            + fileSha1
                        )

                    do! file.CopyToAsync newFile

                    match putObject fileSha1 fileStream with
                    | Error ex -> ()
                    | Ok _ ->
                        Database.CreateFileMeta fileSha1 file.FileName fileStream.Length newFile.Name
                        |> ignore

                        Database.CreateUserFile user.username fileSha1 file.FileName
                        |> ignore

                return! redirectTo false "/file/upload/suc" next ctx
        }

/// 用户文件元数据查询接口
let FileMetaHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        (match ctx.GetQueryStringValue "filehash" with
         | Error msg -> RequestErrors.BAD_REQUEST msg
         | Ok fileHash ->
             match Database.GetFileMetaByHash fileHash with
             | None -> Successful.ok (jsonResp -1 "no such file" "")
             | Some fileMeta -> Successful.ok (json fileMeta))
            next
            ctx


let FileQueryHandler : HttpHandler =
    WithToken
        (fun userTokenBlock (next: HttpFunc) (ctx: HttpContext) ->
            let limit =
                Int32.Parse(
                    ctx.TryGetQueryStringValue "limit"
                    |> Option.defaultValue "5"
                )

            let result = Database.GetLatestFileMetas limit
            Successful.ok (jsonResp 0 "OK" result) next ctx)

let UserFileQueryHandler (userTokenBlock) : HttpHandler =
    (fun (next: HttpFunc) (ctx: HttpContext) ->
        let limit =
            ctx.TryGetQueryStringValue "limit"
            |> Option.defaultValue "5"
            |> Int32.Parse

        let result =
            Database.GetLatestUserFileMetas userTokenBlock.username limit

        Successful.ok (jsonResp 0 "OK" result) next ctx)

/// 文件下载接口
let FileDownloadHandler : HttpHandler =
    bindModel
        None
        (fun user (next: HttpFunc) (ctx: HttpContext) ->
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
                             | Some fileMeta -> Successful.ok (streamFile true fileMeta.Location None None))
                        next
                        ctx
            })

/// 文件更新接口
let FileUpdateHandler : HttpHandler =
    bindModel
        None
        (fun user ->
            tryBindForm
                RequestErrors.BAD_REQUEST
                None
                (fun fileBlock ->
                    if fileBlock.op <> "O" then
                        RequestErrors.METHOD_NOT_ALLOWED ""
                    else
                        match Database.IsUserHaveFile user.username fileBlock.filehash with
                        | false -> RequestErrors.NOT_FOUND "File Not Found"
                        | true ->
                            match Database.GetFileMetaByHash fileBlock.filehash with
                            | None -> RequestErrors.NOT_FOUND "File Not Found"
                            | Some (file: FileMeta) ->
                                let newFile =
                                    { file with
                                          FileName = fileBlock.filename }

                                Database.UpdateFileMeta
                                    newFile.FileSha1
                                    newFile.FileName
                                    newFile.FileSize
                                    newFile.Location
                                |> ignore

                                Successful.ok (json newFile)))

/// 文件删除接口
let FileDeleteHandler : HttpHandler =
    bindModel
        None
        (fun user (next: HttpFunc) (ctx: HttpContext) ->
            (match ctx.GetQueryStringValue "filehash" with
             | Error msg -> RequestErrors.BAD_REQUEST msg
             | Ok fileHash ->
                 match Database.IsUserHaveFile user.username fileHash with
                 | false -> RequestErrors.NOT_FOUND "File Not Found"
                 | true ->
                     match Database.GetFileMetaByHash fileHash with
                     | None -> Successful.ok (jsonResp 0 "ok" "")
                     | Some fileMeta ->
                         File.Delete fileMeta.Location
                         Database.DeleteFileMeta fileHash |> ignore
                         Successful.ok (jsonResp 0 "ok" ""))
                next
                ctx)

/// 用户注册接口
let UserRegister : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let username =
            ctx.GetFormValue "username"
            |> Option.defaultValue ""

        let password =
            ctx.GetFormValue "password"
            |> Option.defaultValue ""

        if username.Length < 3 || password.Length < 5 then
            RequestErrors.BAD_REQUEST "Invalid parameter" next ctx
        else
            let enc_password = Util.EncryptPasswd password

            if Database.UserRegister username enc_password then
                Successful.ok (jsonResp 0 "ok" "") next ctx
            else
                RequestErrors.BAD_REQUEST "Invalid parameter" next ctx

/// 用户登录接口
let UserLogin : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let username =
                ctx.GetFormValue "username"
                |> Option.defaultValue ""

            let password =
                ctx.GetFormValue "password"
                |> Option.defaultValue ""

            let enc_password = Util.EncryptPasswd password

            if Database.UserLogin username enc_password then
                let token = Util.GenToken username

                if Redis.UserUpdateToken username token then
                    let identity =
                        ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme)

                    identity.AddClaim(Claim("user", username))
                    identity.AddClaim(Claim("token", token))
                    identity.AddClaim(Claim("role", "user"))

                    let principal = ClaimsPrincipal(identity)
                    do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal)

                    return!
                        jsonResp
                            0
                            "OK"
                            {| Location =
                                   ctx.Request.Scheme
                                   + "://"
                                   + ctx.Request.Host.Value
                                   + "/"
                               Username = username
                               Token = token |}
                            next
                            ctx
                else
                    return! ServerErrors.SERVICE_UNAVAILABLE "SERVICE_UNAVAILABLE" next ctx
            else
                return! RequestErrors.FORBIDDEN "Wrong password" next ctx
        }

/// 用户注销接口
let UserLogout : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            do! ctx.SignOutAsync()
            return! redirectTo false "/" next ctx
        }

/// 查询用户信息接口
let UserInfoHandler : HttpHandler =
    bindModel
        None
        (fun user ->
            match Database.GetUserByUsername user.username with
            | None -> id
            | Some user -> jsonResp 0 "OK" user)

[<CLIMutable>]
type FastUploadBlock =
    { username: string
      filehash: string
      filename: string
      filesize: Int64 }

let TryFastUploadHandler : HttpHandler =
    tryBindForm
        RequestErrors.BAD_REQUEST
        None
        (fun fastUploadBlock ->
            match Database.GetFileMetaByHash fastUploadBlock.filehash with
            | None -> Successful.ok (jsonResp -1 "秒传失败，请访问普通上传接口" "")
            | Some file ->
                match Database.CreateUserFile fastUploadBlock.username fastUploadBlock.filehash fastUploadBlock.filename with
                | true -> Successful.ok (jsonResp -1 "秒传失败，请访问普通上传接口" "")
                | false -> Successful.ok (jsonResp -2 "秒传失败" ""))

[<CLIMutable>]
type MultipartUploadBlock =
    { username: string
      filehash: string
      filesize: int }

[<CLIMutable>]
type MultipartUploadInfo =
    { FileHash: string
      FileSize: int
      UploadId: string
      ChunkSize: int
      ChunkCount: int }

let InitMultipartUploadHandler : HttpHandler =
    tryBindForm
        RequestErrors.BAD_REQUEST
        None
        (fun (mpUploadBlock: MultipartUploadBlock) ->
            let upInfo =
                { MultipartUploadInfo.FileHash = mpUploadBlock.filehash
                  FileSize = mpUploadBlock.filesize
                  UploadId =
                      Printf.sprintf "%s%x" mpUploadBlock.username (DateTimeOffset(DateTime.Now).ToUnixTimeSeconds())
                  ChunkSize = 5 * 1024 * 1024
                  ChunkCount =
                      float (mpUploadBlock.filesize)
                      / float (5 * 1024 * 1024)
                      |> ceil
                      |> int }

            let redisKey = RedisKey("MP_" + upInfo.UploadId)

            Redis.db.HashSet(redisKey, RedisValue "chunkcount", RedisValue(string upInfo.ChunkCount))
            |> ignore

            Redis.db.HashSet(redisKey, RedisValue "filehash", RedisValue upInfo.FileHash)
            |> ignore

            Redis.db.HashSet(redisKey, RedisValue "filesize", RedisValue(string upInfo.FileSize))
            |> ignore

            Successful.ok (jsonResp 0 "OK" upInfo))

[<CLIMutable>]
type UploadPartBlock =
    { username: string
      uploadid: string
      index: int }

let UploadPartHandler : HttpHandler =
    tryBindForm
        RequestErrors.BAD_REQUEST
        None
        (fun (upPart: UploadPartBlock) (next: HttpFunc) (ctx: HttpContext) ->
            task {
                use body = ctx.Request.BodyReader.AsStream()

                let fPath =
                    Path.Join [| "data/"
                                 upPart.uploadid
                                 string upPart.index |]

                Directory.GetParent(fPath).Create()
                use chunk = File.Create fPath
                do! body.CopyToAsync chunk

                Redis.db.HashSet(
                    RedisKey("MP_" + upPart.uploadid),
                    RedisValue("chkidx_" + string upPart.index),
                    RedisValue(string 1)
                )
                |> ignore

                return! Successful.ok (jsonResp 0 "OK" None) next ctx
            })

[<CLIMutable>]
type CompletePartBlock =
    { uploadid: string
      username: string
      filehash: string
      filesize: int
      filename: string }

let CompleteUploadPartHandler : HttpHandler =
    tryBindForm
        RequestErrors.BAD_REQUEST
        None
        (fun (cpPart: CompletePartBlock) ->
            let data =
                Redis.db.HashGetAll(RedisKey("MP_" + cpPart.uploadid))

            let totalCount =
                data
                |> Array.sumBy
                    (fun entry ->
                        if (string entry.Name).Equals "chunkcount" then
                            int entry.Value
                        else
                            0)

            let chunkCount =
                data
                |> Array.sumBy
                    (fun entry ->
                        if (string entry.Name).StartsWith "chkidx_"
                           && int entry.Value = 1 then
                            1
                        else
                            0)

            if totalCount <> chunkCount then
                Successful.ok (jsonResp -2 "invalid request" null)
            else
                Database.CreateFileMeta cpPart.filehash cpPart.filename (Convert.ToInt64 cpPart.filesize) ""
                Database.CreateUserFile cpPart.username cpPart.filehash cpPart.filename
                Successful.ok (jsonResp 0 "OK" null))

let CancelUploadPartHandler : HttpHandler = id

let MultipartUploadStatusHandler : HttpHandler = id
