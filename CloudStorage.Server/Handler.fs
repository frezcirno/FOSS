module CloudStorage.Server.Handler

open System
open System.IO
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open System.Security.Cryptography
open System.Text
open System.Threading.Tasks
open CloudStorage.Common
open CloudStorage.Storage
open FSharp.Control.Tasks
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Http
open Giraffe
open Microsoft.IdentityModel.JsonWebTokens
open Microsoft.IdentityModel.Tokens
open CloudStorage.Server.Redis
open StackExchange.Redis
open Microsoft.AspNetCore.Authentication

open CloudStorage.Server.MinioOss
//open CloudStorage.Server.AliyunOss

let ArgumentError (err: string) = RequestErrors.BAD_REQUEST err

let jsonResp (code: int) (msg: string) (obj: obj) =
    if obj = null then
        json <| Utils.ResponseBrief code msg
    else
        json <| Utils.Response code msg obj

let okResp (msg: string) (obj: obj) = jsonResp 0 msg obj

///
/// Authentication
///
/// 刷新用户token
let UserUpdateToken (user_name: string) (user_token: string) : bool =
    redis.StringSet(RedisKey(user_name), RedisValue(user_token), TimeSpan.FromHours(1.0))

let UserValidToken (user_name: string) (user_token: string) : bool =
    redis.StringGet(RedisKey(user_name)) = RedisValue(user_token)

let EncryptPasswd =
    Utils.flip (+) Config.Security.Salt
    >> Utils.StringSha1

let notLoggedIn : HttpHandler =
    RequestErrors.UNAUTHORIZED "Cookie" "SAFE Realm" "You must be logged in."

let jwtAuthorized : HttpHandler =
    requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme)

let cookieAuthorized : HttpHandler =
    requiresAuthentication (challenge CookieAuthenticationDefaults.AuthenticationScheme)

///
/// Storage Backend
///
let private SaveFileAsync (fileHash: string) (fileName: string) (fileLength: int64) (stream: Stream) =
    task {
        if Database.File.FileHashExists fileHash then
            return true
        else
            do! putObjectAsync fileHash stream
            return Database.File.CreateFileMeta fileHash fileName fileLength fileHash
    }



/// 用户上传文件
let FileUploadHandler (next: HttpFunc) (ctx: HttpContext) =
    task {
        let username = ctx.User.FindFirstValue ClaimTypes.Name

        if ctx.Request.Form.Files.Count = 1 then
            let file = ctx.Request.Form.Files.[0]

            let stream = file.OpenReadStream()
            let fileHash = Utils.StreamSha1 stream

            stream.Seek(0L, SeekOrigin.Begin) |> ignore

            let! saveResult = SaveFileAsync fileHash file.Name file.Length stream

            if saveResult then
                if Database.UserFile.CreateUserFile username fileHash file.FileName then
                    return! okResp "OK" null next ctx
                else
                    return! ServerErrors.SERVICE_UNAVAILABLE "CreateUserFile" next ctx
            else
                return! ServerErrors.SERVICE_UNAVAILABLE "saveResult" next ctx
        else
            return! ArgumentError "File Count Exceed!" next ctx
    }

/// 文件元数据查询接口
let FileMetaHandler (next: HttpFunc) (ctx: HttpContext) =
    match ctx.GetQueryStringValue "fileHash" with
    | Error msg -> ArgumentError msg next ctx
    | Ok fileHash ->
        match Database.File.GetFileMetaByHash fileHash with
        | None -> okResp "OK" [] next ctx
        | Some fileMeta -> okResp "OK" fileMeta next ctx

[<CLIMutable>]
type RecentFileBlock = { page: int; limit: int }

/// 最近上传文件查询接口
let RecentFileHandler (next: HttpFunc) (ctx: HttpContext) =
    let username = ctx.User.FindFirstValue ClaimTypes.Name

    match ctx.TryBindQueryString<RecentFileBlock>() with
    | Error msg -> ArgumentError msg next ctx
    | Ok args ->
        let result =
            Database.UserFile.GetUserFiles username args.page args.limit

        okResp "OK" result next ctx

/// 用户文件查询接口
let UserFileQueryHandler (next: HttpFunc) (ctx: HttpContext) =
    let username = ctx.User.FindFirstValue ClaimTypes.Name

    let limit =
        ctx.TryGetQueryStringValue "limit"
        |> Option.defaultValue "5"
        |> Int32.Parse

    let result =
        Database.UserFile.GetUserFiles username limit

    okResp "OK" result next ctx

/// 文件下载接口
/// 用户登录之后根据 filename 下载文件
let FileDownloadHandler (next: HttpFunc) (ctx: HttpContext) =
    let username = ctx.User.FindFirstValue ClaimTypes.Name

    match ctx.GetQueryStringValue "filename" with
    | Error msg -> ArgumentError msg next ctx
    | Ok fileName ->
        /// 查询用户文件记录
        match Database.UserFile.GetUserFileByFileName username fileName with
        | None -> RequestErrors.NOT_FOUND "File Not Found" next ctx
        | Some userFile ->
            /// 获取文件
            match Database.File.GetFileMetaByHash userFile.FileHash with
            | None -> RequestErrors.NOT_FOUND "File Not Found" next ctx
            | Some fileMeta ->
                use data = getObject fileMeta.FileLoc
                streamData true data None None next ctx

/// 文件更新接口
/// 用户登录之后通过此接口修改文件元信息
let FileUpdateHandler (next: HttpFunc) (ctx: HttpContext) =
    let username = ctx.User.FindFirstValue ClaimTypes.Name

    let fileName =
        ctx.GetFormValue "filename"
        |> Option.defaultValue ""

    let op =
        ctx.GetFormValue "op" |> Option.defaultValue ""

    if op = "rename" then
        match ctx.GetFormValue "newName" with
        | None -> ArgumentError "new name is needed" next ctx
        | Some newName ->
            if
                newName.Length <> 0
                && not (Database.UserFile.IsUserHaveFile username newName)
            then
                match Database.UserFile.GetUserFileByFileName username fileName with
                | None -> RequestErrors.NOT_FOUND "File Not Found" next ctx
                | Some userFile ->
                    let newUserFile = { userFile with FileName = newName }

                    if Database.UserFile.UpdateUserFileByUserFile username fileName newUserFile then
                        json newUserFile next ctx
                    else
                        ServerErrors.SERVICE_UNAVAILABLE id next ctx
            else
                ArgumentError "invalid new name" next ctx
    else
        ServerErrors.NOT_IMPLEMENTED id next ctx

/// 文件删除接口
/// 用户登录之后根据 filename 删除文件
let FileDeleteHandler (next: HttpFunc) (ctx: HttpContext) =
    let username = ctx.User.FindFirstValue ClaimTypes.Name

    match ctx.GetQueryStringValue "fileName" with
    | Error msg -> ArgumentError msg next ctx
    | Ok fileName ->
        /// 查询用户文件
        if not (Database.UserFile.IsUserHaveFile username fileName) then
            RequestErrors.NOT_FOUND "File Not Found" next ctx
        else
        /// 删除用户与文件之间的关联
        if Database.UserFile.DeleteUserFileByFileName username fileName then
            okResp "OK" null next ctx
        else
            ServerErrors.SERVICE_UNAVAILABLE id next ctx

[<CLIMutable>]
type UserRegisterBlock = { username: string; password: string }

/// 用户注册接口
let UserRegister (next: HttpFunc) (ctx: HttpContext) =
    task {
        match! ctx.TryBindFormAsync<UserRegisterBlock>() with
        | Error msg -> return! ArgumentError msg next ctx
        | Ok args ->
            if args.username.Length < 3
               || args.password.Length < 5 then
                return! ArgumentError "Invalid parameter" next ctx
            else
                let enc_password = EncryptPasswd args.password

                if Database.User.UserRegister args.username enc_password then
                    return! okResp "OK" null next ctx
                else
                    return! ServerErrors.serviceUnavailable id next ctx
    }

let BuildToken (username: string) =
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
    writeToken

[<CLIMutable>]
type UserLoginBlock = { username: string; password: string }

/// 用户登录接口
let UserLogin (next: HttpFunc) (ctx: HttpContext) =
    task {
        match! ctx.TryBindFormAsync<UserLoginBlock>() with
        | Error msg -> return! ArgumentError msg next ctx
        | Ok args ->
            let enc_password = EncryptPasswd args.password

            if Database.User.GetUserByUsernameAndUserPwd args.username enc_password then
                let token = BuildToken(args.username)

                if UserUpdateToken args.username token then
                    let ret =
                        {| FileLoc =
                               ctx.Request.Scheme
                               + "://"
                               + ctx.Request.Host.Value
                               + "/"
                           Username = args.username
                           AccessToken = token |}

                    return! okResp "OK" ret next ctx
                else
                    return! ServerErrors.SERVICE_UNAVAILABLE "SERVICE_UNAVAILABLE" next ctx
            else
                return! RequestErrors.FORBIDDEN "Wrong password" next ctx
    }

/// 用户注销接口
let UserLogout (next: HttpFunc) (ctx: HttpContext) =
    task {
        do! ctx.SignOutAsync()
        return! redirectTo false "/" next ctx
    }

/// 用户信息查询接口
let UserInfoHandler (next: HttpFunc) (ctx: HttpContext) =
    let username = ctx.User.FindFirstValue ClaimTypes.Name

    match Database.User.GetUserByUsername username with
    | None -> ServerErrors.INTERNAL_ERROR "User not found" next ctx
    | Some user -> okResp "OK" user next ctx

[<CLIMutable>]
type FastUploadInitBlock =
    { username: string
      fileHash: string
      fileName: string
      fileSize: int64 }

///
/// 尝试秒传
///
let TryFastUploadHandler (next: HttpFunc) (ctx: HttpContext) =
    task {
        match! ctx.TryBindFormAsync<FastUploadInitBlock>() with
        | Error msg -> return! ArgumentError msg next ctx
        | Ok initBlock ->
            // 查询文件是否存在
            if not (Database.File.FileHashExists initBlock.fileHash) then
                return! jsonResp -1 "秒传失败，请访问普通上传接口" null next ctx
            /// 尝试秒传
            else if Database.UserFile.CreateUserFile initBlock.username initBlock.fileHash initBlock.fileName then
                return! okResp "OK" null next ctx
            else
                return! ServerErrors.SERVICE_UNAVAILABLE "秒传服务暂不可用，请访问普通上传接口" next ctx
    }

[<CLIMutable>]
type MultipartUploadBlock =
    { username: string
      fileHash: string
      fileSize: int64 }

[<CLIMutable>]
type MultipartInfo =
    { FileHash: string
      FileSize: int64
      UploadId: string
      ChunkSize: int
      ChunkCount: int }

///
/// 初始化文件分块上传接口
///
let InitMultipartUploadHandler (next: HttpFunc) (ctx: HttpContext) =
    task {
        match! ctx.TryBindFormAsync<MultipartUploadBlock>() with
        | Error msg -> return! ArgumentError msg next ctx
        | Ok args ->
            let mpInfo =
                { FileHash = args.fileHash
                  FileSize = args.fileSize
                  UploadId = Utils.StringSha1(args.username + args.fileHash + Guid().ToString())
                  ChunkSize = Config.CHUNK_SIZE
                  ChunkCount =
                      float args.fileSize / float Config.CHUNK_SIZE
                      |> ceil
                      |> int }

            /// 每个文件的所有part记录在一个hashmap中
            let mpKey = RedisKey("MP_" + mpInfo.UploadId)

            redis.HashSet(mpKey, RedisValue("chunkCount"), RedisValue(string mpInfo.ChunkCount))
            |> ignore

            redis.HashSet(mpKey, RedisValue("fileHash"), RedisValue(mpInfo.FileHash))
            |> ignore

            redis.HashSet(mpKey, RedisValue("fileSize"), RedisValue(string mpInfo.FileSize))
            |> ignore

            redis.KeyExpire(mpKey, TimeSpan.FromHours(12.0))
            |> ignore

            //            redis.StringSet()

            return! okResp "OK" mpInfo next ctx
    }

[<CLIMutable>]
type UploadPartBlock =
    { username: string
      uploadId: string
      index: int }

///
/// 上传一个分片
///
let UploadPartHandler (next: HttpFunc) (ctx: HttpContext) =
    task {
        match! ctx.TryBindFormAsync<UploadPartBlock>() with
        | Error msg -> return! ArgumentError msg next ctx
        | Ok partInfo ->
            use body = ctx.Request.Body

            /// 将分片保存到 TEMP/upId/index
            let fPath =
                Path.Join [| Config.TEMP_FILE_PATH
                             partInfo.uploadId
                             string partInfo.index |]

            Directory.GetParent(fPath).Create()
            use chunk = File.Create fPath
            do! body.CopyToAsync chunk

            /// 每一个分片完成就在hashmap中添加一项
            let mpKey = RedisKey("MP_" + partInfo.uploadId)

            Redis.redis.HashSet(mpKey, RedisValue("chkidx_" + string partInfo.index), RedisValue(string 1))
            |> ignore

            return! okResp "OK" None next ctx
    }

let AppendStream (os: Stream) (is: Stream) : Task<Stream> =
    task {
        do! is.CopyToAsync os
        return os
    }

let MergePartsAsync (fPath: string) (chunkCount: int) : Task<Stream> =
    use stream = new MemoryStream()

    let output =
        [ 0 .. chunkCount ]
        |> List.fold
            (fun (os: Task<Stream>) (index: int) ->
                os.ContinueWith
                    (fun (task: Task<Stream>) ->
                        use file =
                            File.OpenRead(Path.Join [| fPath; string index |])

                        (AppendStream task.Result file).Result))
            (Task.FromResult(stream :> Stream))

    output

[<CLIMutable>]
type CompletePartBlock =
    { uploadId: string
      username: string
      fileHash: string
      fileSize: int64
      fileName: string }

///
/// 分片上传完成接口
///
let CompleteUploadPartHandler (next: HttpFunc) (ctx: HttpContext) =
    task {
        match! ctx.TryBindFormAsync<CompletePartBlock>() with
        | Error msg -> return! ArgumentError msg next ctx
        | Ok args ->
            let data =
                Redis.redis.HashGetAll(RedisKey("MP_" + args.uploadId))

            let totalCount =
                let count =
                    data
                    |> Array.find (fun entry -> entry.Name.ToString() = "chunkcount")

                int count.Value

            let uploadCount =
                let count =
                    data
                    |> Array.filter (fun entry -> entry.Name.ToString().StartsWith "chkidx_")

                count.Length

            if totalCount <> uploadCount then
                return! jsonResp -2 "invalid request" null next ctx
            else

                let fPath =
                    Path.Join [| Config.TEMP_FILE_PATH
                                 args.uploadId |]

                let! mergeStream = MergePartsAsync fPath totalCount

                let! saveRes = SaveFileAsync args.fileHash args.fileName args.fileSize mergeStream

                if not saveRes then
                    return! ServerErrors.SERVICE_UNAVAILABLE "CreateFileMeta" next ctx
                else if not (Database.UserFile.CreateUserFile args.username args.fileHash args.fileName) then
                    return! ServerErrors.SERVICE_UNAVAILABLE "CreateUserFile" next ctx
                else
                    return! okResp "OK" null next ctx
    }

let CancelUploadPartHandler : HttpHandler = id

let MultipartUploadStatusHandler : HttpHandler = id

let routes : HttpHandler =
    choose [ route "/" >=> htmlFile "static/index.html"
             route "/ping" >=> Successful.OK "pong!"
             route "/auth/ping"
             >=> jwtAuthorized
             >=> Successful.OK "pong!"
             route "/time"
             >=> warbler (fun _ -> text (DateTime.Now.ToString()))
             route "/test"
             >=> RequestErrors.UNAUTHORIZED "" "" ""

             route "/user/signup" >=> UserRegister
             route "/user/signin" >=> UserLogin
             route "/user/signout" >=> UserLogout
             route "/user/info"
             >=> jwtAuthorized
             >=> UserInfoHandler

             route "/file/upload"
             >=> choose [ POST >=> jwtAuthorized >=> FileUploadHandler
                          RequestErrors.METHOD_NOT_ALLOWED id ]
             route "/file/meta"
             >=> jwtAuthorized
             >=> FileMetaHandler
             route "/file/recent"
             >=> jwtAuthorized
             >=> RecentFileHandler
             route "/file/download"
             >=> jwtAuthorized
             >=> FileDownloadHandler
             route "/file/update"
             >=> jwtAuthorized
             >=> FileUpdateHandler
             route "/file/delete"
             >=> jwtAuthorized
             >=> FileDeleteHandler

             route "/file/fastupload"
             >=> jwtAuthorized
             >=> TryFastUploadHandler
             route "/file/mpupload/init"
             >=> jwtAuthorized
             >=> InitMultipartUploadHandler
             route "/file/mpupload/uppart"
             >=> jwtAuthorized
             >=> UploadPartHandler
             route "/file/mpupload/complete"
             >=> jwtAuthorized
             >=> CompleteUploadPartHandler
             route "/file/mpupload/cancel"
             >=> jwtAuthorized
             >=> CancelUploadPartHandler
             route "/file/mpupload/status"
             >=> jwtAuthorized
             >=> MultipartUploadStatusHandler

             route "/error" >=> htmlFile "static/error.html"

             RequestErrors.notFound (text "Not Found") ]
