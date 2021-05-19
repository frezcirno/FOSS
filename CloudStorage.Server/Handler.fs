module CloudStorage.Server.Handler

open System
open System.IO
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open System.Text
open CloudStorage.Storage
open FSharp.Control.Tasks.V2.ContextInsensitive
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Http
open Giraffe
open Microsoft.IdentityModel.JsonWebTokens
open Microsoft.IdentityModel.Tokens
open StackExchange.Redis
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

type UploadBlock =
    { FileName: string
      FileSize: int64
      content: string }

let private InnerFileUpload (fileHash: string) (fileName: string) (fileLength: Int64) (stream: Stream) =
    task {
        if Database.File.FileHashExists fileHash then
            return true
        else
            stream.Seek(0L, SeekOrigin.Begin) |> ignore
            do! Storage.putObjectAsync fileHash stream
            return Database.File.CreateFileMeta fileHash fileName fileLength fileHash
    }

/// 用户上传文件
let FileUploadHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let username = ctx.User.FindFirstValue ClaimTypes.Name

            if ctx.Request.Form.Files.Count = 1 then
                let file = ctx.Request.Form.Files.[0]

                let stream = file.OpenReadStream()
                let fileHash = Util.StreamSha1 stream

                let! saveResult = InnerFileUpload fileHash file.Name file.Length stream

                if saveResult then
                    if Database.UserFile.CreateUserFile username fileHash file.FileName then
                        return! jsonResp 0 "ok" null next ctx
                    else
                        return! ServerErrors.SERVICE_UNAVAILABLE "CreateUserFile" next ctx
                else
                    return! ServerErrors.SERVICE_UNAVAILABLE "saveResult" next ctx
            else
                return! RequestErrors.BAD_REQUEST "Bad request" next ctx
        }

/// 文件元数据查询接口
let FileMetaHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        match ctx.GetQueryStringValue "filehash" with
        | Error msg -> RequestErrors.BAD_REQUEST msg next ctx
        | Ok fileHash ->
            match Database.File.GetFileMetaByHash fileHash with
            | None -> jsonResp 0 "ok" [] next ctx
            | Some fileMeta -> jsonResp 0 "ok" fileMeta next ctx

/// 最近上传文件查询接口
let RecentFileHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let username = ctx.User.FindFirstValue ClaimTypes.Name

        let page =
            ctx.TryGetQueryStringValue "page"
            |> Option.defaultValue "0"
            |> Int32.Parse

        let limit =
            ctx.TryGetQueryStringValue "limit"
            |> Option.defaultValue "5"
            |> Int32.Parse

        let result =
            Database.UserFile.GetUserFiles username page limit

        jsonResp 0 "OK" result next ctx

/// 用户文件查询接口
let UserFileQueryHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let username = ctx.User.FindFirstValue ClaimTypes.Name

        let limit =
            ctx.TryGetQueryStringValue "limit"
            |> Option.defaultValue "5"
            |> Int32.Parse

        let result =
            Database.UserFile.GetUserFiles username limit

        jsonResp 0 "OK" result next ctx

/// 文件下载接口
/// 用户登录之后根据 filename 下载文件
let FileDownloadHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let username = ctx.User.FindFirstValue ClaimTypes.Name

        match ctx.GetQueryStringValue "filename" with
        | Ok fileName ->
            match Database.UserFile.GetUserFileByFileName username fileName with
            | Some userFile ->
                match Database.File.GetFileMetaByHash userFile.FileHash with
                | Some fileMeta -> streamData true (Storage.getObject fileMeta.FileLoc) None None next ctx
                | _ -> RequestErrors.NOT_FOUND "File Not Found" next ctx
            | _ -> RequestErrors.NOT_FOUND "File Not Found" next ctx
        | Result.Error msg -> RequestErrors.BAD_REQUEST msg next ctx

/// 文件更新接口
/// 用户登录之后通过此接口修改文件元信息
let FileUpdateHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let username = ctx.User.FindFirstValue ClaimTypes.Name

        let fileName =
            ctx.GetFormValue "filename"
            |> Option.defaultValue ""

        let op =
            ctx.GetFormValue "op" |> Option.defaultValue ""

        if op = "rename" then
            match ctx.GetFormValue "newname" with
            | Some newName ->
                if
                    newName.Length <> 0
                    && not (Database.UserFile.IsUserHaveFile username newName)
                then
                    match Database.UserFile.GetUserFileByFileName username fileName with
                    | Some userFile ->
                        let newUserFile = { userFile with FileName = newName }

                        if Database.UserFile.UpdateUserFileByUserFile username fileName newUserFile then
                            json newUserFile next ctx
                        else
                            ServerErrors.SERVICE_UNAVAILABLE "" next ctx
                    | _ -> RequestErrors.NOT_FOUND "File Not Found" next ctx
                else
                    RequestErrors.BAD_REQUEST "invalid newname" next ctx
            | _ -> RequestErrors.BAD_REQUEST "newname is needed" next ctx
        else
            ServerErrors.NOT_IMPLEMENTED "NOT_IMPLEMENTED" next ctx

/// 文件删除接口
/// 用户登录之后根据 filename 删除文件
let FileDeleteHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let username = ctx.User.FindFirstValue ClaimTypes.Name

        match ctx.GetQueryStringValue "filename" with
        | Error msg -> RequestErrors.BAD_REQUEST msg next ctx
        | Ok fileName ->
            match Database.UserFile.GetUserFileByFileName username fileName with
            | Some userFile ->
                if Database.UserFile.DeleteUserFileByFileName username fileName then
                    jsonResp 0 "ok" "" next ctx
                else
                    ServerErrors.SERVICE_UNAVAILABLE "" next ctx
            | _ -> RequestErrors.NOT_FOUND "File Not Found" next ctx

/// 用户注册接口
let UserRegister : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        match ctx.GetFormValue "username", ctx.GetFormValue "password" with
        | Some username, Some password ->
            if username.Length < 3 || password.Length < 5 then
                RequestErrors.BAD_REQUEST "Invalid parameter" next ctx
            else
                let enc_password = Util.EncryptPasswd password

                if Database.User.UserRegister username enc_password then
                    jsonResp 0 "ok" "" next ctx
                else
                    RequestErrors.BAD_REQUEST "Invalid parameter" next ctx

        | _ -> RequestErrors.BAD_REQUEST "Invalid parameter" next ctx

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

            if Database.User.UserLogin username enc_password then

                let tokenHandler = JwtSecurityTokenHandler()
                let tokenDescriptor = SecurityTokenDescriptor()

                tokenDescriptor.Subject <-
                    ClaimsIdentity(
                        [| Claim(JwtRegisteredClaimNames.Aud, "api")
                           Claim(JwtRegisteredClaimNames.Iss, "http://7c00h.xyz/cloud")
                           Claim(ClaimTypes.Name, username) |],
                        JwtBearerDefaults.AuthenticationScheme
                    )

                tokenDescriptor.Expires <- DateTime.UtcNow.AddHours(1.0)

                tokenDescriptor.SigningCredentials <-
                    SigningCredentials(
                        SymmetricSecurityKey(Encoding.ASCII.GetBytes Config.Security.Secret),
                        SecurityAlgorithms.HmacSha256Signature
                    )

                let securityToken = tokenHandler.CreateToken tokenDescriptor
                let writeToken = tokenHandler.WriteToken securityToken


                if Redis.UserUpdateToken username writeToken then
                    //                    let identity =
//                        ClaimsIdentity(
//                            [| Claim("user", username)
//                               Claim("token", token)
//                               Claim("role", "user") |],
//                            JwtBearerDefaults.AuthenticationScheme
//                        )
//
//                    let principal = ClaimsPrincipal(identity)
//                    do! ctx.SignInAsync(JwtBearerDefaults.AuthenticationScheme, principal)

                    return!
                        jsonResp
                            0
                            "OK"
                            {| FileLoc =
                                   ctx.Request.Scheme
                                   + "://"
                                   + ctx.Request.Host.Value
                                   + "/"
                               Username = username
                               AccessToken = writeToken |}
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

/// 用户信息查询接口
let UserInfoHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let username = ctx.User.FindFirstValue ClaimTypes.Name

        match Database.User.GetUserByUsername username with
        | None -> ServerErrors.INTERNAL_ERROR "User not found" next ctx
        | Some user -> jsonResp 0 "OK" user next ctx

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
            match Database.File.GetFileMetaByHash fastUploadBlock.filehash with
            | None -> Successful.ok (jsonResp -1 "秒传失败，请访问普通上传接口" "")
            | Some file ->
                match Database.UserFile.CreateUserFile
                          fastUploadBlock.username
                          fastUploadBlock.filehash
                          fastUploadBlock.filename with
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
                Database.File.CreateFileMeta cpPart.filehash cpPart.filename (Convert.ToInt64 cpPart.filesize) ""
                Database.UserFile.CreateUserFile cpPart.username cpPart.filehash cpPart.filename
                Successful.ok (jsonResp 0 "OK" null))

let CancelUploadPartHandler : HttpHandler = id

let MultipartUploadStatusHandler : HttpHandler = id
