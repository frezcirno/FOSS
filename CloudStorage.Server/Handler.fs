module CloudStorage.Server.Handler

open System
open System.IO
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open System.Text
open CloudStorage.Common
open FSharp.Control.Tasks
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Http
open Giraffe
open Microsoft.IdentityModel.JsonWebTokens
open Microsoft.IdentityModel.Tokens
open StackExchange.Redis
open Microsoft.AspNetCore.Authentication

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
    Redis.redis.StringSet(RedisKey(user_name), RedisValue(user_token), TimeSpan.FromHours(1.0))

let UserValidToken (user_name: string) (user_token: string) : bool =
    Redis.redis.StringGet(RedisKey(user_name)) = RedisValue(user_token)

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
let private SaveFile (fileHash: string) (fileName: string) (fileLength: int64) (stream: Stream) =
    if Database.File.FileHashExists fileHash then
        true
    else
        let tempPath =
            Path.Join [| Config.TEMP_FILE_PATH
                         fileHash |]

        let os = File.OpenWrite tempPath
        stream.CopyTo os
        os.Dispose()

        /// 发布消息
        let msg =
            RabbitMsg(
                FileHash = fileHash,
                CurLocation = tempPath,
                DstLocation = fileHash,
                DstType = RabbitMsg.Types.DstType.Minio
            )

        RabbitMq.Publish Config.Rabbit.TransExchangeName Config.Rabbit.TransRoutingKey msg
        Database.File.CreateFileMeta fileHash fileName fileLength fileHash

module Upload =
    /// 用户上传文件
    let FileUploadHandler (next: HttpFunc) (ctx: HttpContext) =
        let username = ctx.User.FindFirstValue ClaimTypes.Name

        if ctx.Request.Form.Files.Count = 1 then
            let file = ctx.Request.Form.Files.[0]

            let stream = file.OpenReadStream()
            let fileHash = Utils.StreamSha1 stream

            stream.Seek(0L, SeekOrigin.Begin) |> ignore

            let saveResult =
                SaveFile fileHash file.Name file.Length stream

            if saveResult then
                if Database.UserFile.CreateUserFile username fileHash file.FileName file.Length then
                    okResp "OK" null next ctx
                else
                    ServerErrors.SERVICE_UNAVAILABLE "CreateUserFile" next ctx
            else
                ServerErrors.SERVICE_UNAVAILABLE "saveResult" next ctx
        else
            ArgumentError "File Count Exceed!" next ctx

    /// 文件元数据查询接口
    let FileMetaHandler (next: HttpFunc) (ctx: HttpContext) =
        match ctx.GetQueryStringValue "fileName" with
        | Error msg -> ArgumentError msg next ctx
        | Ok fileName ->
            match Database.File.GetFileMetaByFileName fileName with
            | None -> RequestErrors.notFound id next ctx
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
                    use data = MinioOss.getObject fileMeta.FileLoc
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

module User =
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

module MpUpload =
    [<CLIMutable>]
    type FastUploadInitBlock =
        { fileHash: string
          fileName: string
          fileSize: int64 }

    ///
    /// 尝试秒传
    ///
    let TryFastUploadHandler (next: HttpFunc) (ctx: HttpContext) =
        task {
            let username = ctx.User.FindFirstValue ClaimTypes.Name

            match! ctx.TryBindFormAsync<FastUploadInitBlock>() with
            | Error msg -> return! ArgumentError msg next ctx
            | Ok initBlock ->
                // 查询文件是否存在
                if not (Database.File.FileHashExists initBlock.fileHash) then
                    return! jsonResp -1 "秒传失败，请访问普通上传接口" null next ctx
                /// 尝试秒传
                else if Database.UserFile.CreateUserFile
                            username
                            initBlock.fileHash
                            initBlock.fileName
                            initBlock.fileSize then
                    return! okResp "OK" null next ctx
                else
                    return! ServerErrors.SERVICE_UNAVAILABLE "秒传服务暂不可用，请访问普通上传接口" next ctx
        }

    [<CLIMutable>]
    type MultipartUploadBlock = { fileHash: string; fileSize: int64 }

    [<CLIMutable>]
    type MultipartInfo =
        { FileHash: string
          FileSize: int64
          UploadId: string
          ChunkSize: int
          ChunkCount: int
          ChunkExists: int [] }

    ///
    /// 初始化文件分块上传接口
    ///
    let InitMultipartUploadHandler (next: HttpFunc) (ctx: HttpContext) =
        task {
            let username = ctx.User.FindFirstValue ClaimTypes.Name

            match! ctx.TryBindFormAsync<MultipartUploadBlock>() with
            | Error msg -> return! ArgumentError msg next ctx
            | Ok args ->
                let uploadId =
                    /// 如果 HASH_KEY_PREFIX + hash -> uploadId 存在，说明是断点续传
                    let key =
                        RedisKey(Config.HASH_KEY_PREFIX + args.fileHash)

                    match Redis.redis.KeyExists key with
                    /// 不存在则创建一个新的
                    | false -> Utils.StringSha1(username + args.fileHash + Guid().ToString())
                    | true -> Redis.redis.StringGet(key).ToString()

                /// 存在则获取chunk info
                let chunks =
                    Redis.redis.ListRange(RedisKey(Config.CHUNK_KEY_PREFIX + uploadId))
                    |> Array.map int

                let ret =
                    { FileHash = args.fileHash
                      FileSize = args.fileSize
                      UploadId = uploadId
                      ChunkSize = Config.CHUNK_SIZE
                      ChunkCount =
                          float args.fileSize / float Config.CHUNK_SIZE
                          |> ceil
                          |> int
                      ChunkExists = chunks }

                ///
                /// 如果没有已经上传的chunk, 说明是第一次上传
                /// 初始化断点信息到redis
                ///
                if chunks.Length = 0 then
                    let fPath =
                        Path.Join [| Config.TEMP_FILE_PATH
                                     uploadId |]

                    Directory.CreateDirectory fPath |> ignore

                    let mpKey =
                        RedisKey(Config.UPLOAD_INFO_KEY_PREFIX + ret.UploadId)

                    Redis.redis.HashSet(mpKey, RedisValue("chunkCount"), RedisValue(string ret.ChunkCount))
                    |> ignore

                    Redis.redis.HashSet(mpKey, RedisValue("fileHash"), RedisValue(ret.FileHash))
                    |> ignore

                    Redis.redis.HashSet(mpKey, RedisValue("fileSize"), RedisValue(string ret.FileSize))
                    |> ignore

                    Redis.redis.KeyExpire(mpKey, TimeSpan.FromHours(12.0))
                    |> ignore

                    Redis.redis.StringSet(
                        RedisKey(Config.HASH_KEY_PREFIX + ret.FileHash),
                        RedisValue(ret.UploadId),
                        TimeSpan.FromHours(12.0)
                    )
                    |> ignore

                return! okResp "OK" ret next ctx
        }

    [<CLIMutable>]
    type UploadPartBlock = { uploadId: string; index: int }

    ///
    /// 上传一个分片
    ///
    let UploadPartHandler (next: HttpFunc) (ctx: HttpContext) =
        task {
            match ctx.TryBindQueryString<UploadPartBlock>() with
            | Error msg -> return! ArgumentError msg next ctx
            | Ok partInfo ->
                let data = ctx.Request.Body

                /// 将分片保存到 TEMP/upId/index
                let fPath =
                    Path.Join [| Config.TEMP_FILE_PATH
                                 partInfo.uploadId
                                 string partInfo.index |]

                Directory.GetParent(fPath).Create()
                use chunk = File.Create fPath
                do! data.CopyToAsync chunk

                ///
                /// 每一个分片完成就在 CHUNK_KEY_PREFIX + uploadId -> [  ] 中添加一项 CHUNK_PREFIX + index
                ///
                let chunkKey =
                    RedisKey(Config.CHUNK_KEY_PREFIX + partInfo.uploadId)

                let isExist =
                    Redis.redis.ListRange chunkKey
                    |> Array.exists (fun v -> (int v) = partInfo.index)

                if not isExist then
                    Redis.redis.ListRightPush(chunkKey, RedisValue(string partInfo.index))
                    |> ignore

                return! okResp "OK" None next ctx
        }

    /// Merge all chunks and return a big stream
    let MergeParts (fPath: string) (chunkCount: int) =
        let stream = new MemoryStream()

        for index in [ 0 .. chunkCount - 1 ] do
            use file =
                File.OpenRead(Path.Join [| fPath; string index |])

            file.CopyTo stream

        stream.Seek(0L, SeekOrigin.Begin) |> ignore
        stream

    [<CLIMutable>]
    type CompletePartBlock =
        { uploadId: string
          fileHash: string
          fileSize: int64
          fileName: string }

    ///
    /// 分片上传完成接口
    ///
    let CompleteUploadPartHandler (next: HttpFunc) (ctx: HttpContext) =
        task {
            let username = ctx.User.FindFirstValue ClaimTypes.Name

            match! ctx.TryBindFormAsync<CompletePartBlock>() with
            | Error msg -> return! ArgumentError msg next ctx
            | Ok args ->
                let totalCount =
                    Redis.redis.HashGet(
                        RedisKey(Config.UPLOAD_INFO_KEY_PREFIX + args.uploadId),
                        RedisValue("chunkCount")
                    )
                    |> int

                let uploadCount =
                    Redis.redis.ListLength(RedisKey(Config.CHUNK_KEY_PREFIX + args.uploadId))
                    |> int

                if totalCount <> uploadCount then
                    return! jsonResp -2 "invalid request" null next ctx
                else

                    let fPath =
                        Path.Join [| Config.TEMP_FILE_PATH
                                     args.uploadId |]

                    use mergeStream = MergeParts fPath totalCount

                    let saveFileResult =
                        SaveFile args.fileHash args.fileName args.fileSize mergeStream

                    if not saveFileResult then
                        return! ServerErrors.SERVICE_UNAVAILABLE "CreateFileMeta" next ctx
                    elif not (Database.UserFile.CreateUserFile username args.fileHash args.fileName args.fileSize) then
                        return! ServerErrors.SERVICE_UNAVAILABLE "CreateUserFile" next ctx
                    else

                        let hashKey =
                            RedisKey(Config.HASH_KEY_PREFIX + args.fileHash)

                        if Redis.redis.KeyExists hashKey then
                            let uploadId =
                                Redis.redis.StringGet(hashKey).ToString()

                            let fPath =
                                Path.Join [| Config.TEMP_FILE_PATH
                                             uploadId |]

                            Directory.Delete(fPath, true)

                            Redis.redis.KeyDelete(hashKey) |> ignore

                            Redis.redis.KeyDelete(RedisKey(Config.UPLOAD_INFO_KEY_PREFIX + uploadId))
                            |> ignore

                            Redis.redis.KeyDelete(RedisKey(Config.CHUNK_KEY_PREFIX + uploadId))
                            |> ignore

                        return! okResp "OK" null next ctx
        }

    let CancelUploadPartHandler (next: HttpFunc) (ctx: HttpContext) =
        task {
            match ctx.GetFormValue "fileHash" with
            | None -> return! ArgumentError "fileHash" next ctx
            | Some fileHash ->
                let hashKey =
                    RedisKey(Config.HASH_KEY_PREFIX + fileHash)

                if not (Redis.redis.KeyExists hashKey) then
                    return! okResp "Nothing found" null next ctx
                else
                    let uploadId =
                        Redis.redis.StringGet(hashKey).ToString()

                    let fPath =
                        Path.Join [| Config.TEMP_FILE_PATH
                                     uploadId |]

                    Directory.Delete(fPath, true)

                    Redis.redis.KeyDelete(hashKey) |> ignore

                    Redis.redis.KeyDelete(RedisKey(Config.UPLOAD_INFO_KEY_PREFIX + uploadId))
                    |> ignore

                    Redis.redis.KeyDelete(RedisKey(Config.CHUNK_KEY_PREFIX + uploadId))
                    |> ignore

                    return! okResp "OK" "Success delete" next ctx
        }

    let MultipartUploadStatusHandler (next: HttpFunc) (ctx: HttpContext) =
        task {
            match ctx.GetFormValue "fileHash" with
            | None -> return! ArgumentError "file hash is needed" next ctx
            | Some fileHash ->
                let hashKey =
                    RedisKey(Config.HASH_KEY_PREFIX + fileHash)

                if not (Redis.redis.KeyExists hashKey) then
                    return! RequestErrors.notFound id next ctx
                else
                    let uploadId =
                        Redis.redis.StringGet(hashKey).ToString()

                    let chunks =
                        Redis.redis.ListRange(RedisKey(Config.CHUNK_KEY_PREFIX + uploadId))
                        |> Array.map (fun x -> x.ToString() |> int)

                    let mpKey =
                        RedisKey(Config.UPLOAD_INFO_KEY_PREFIX + uploadId)

                    let ret =
                        { FileHash = fileHash
                          FileSize =
                              Redis
                                  .redis
                                  .HashGet(mpKey, RedisValue("fileSize"))
                                  .ToString()
                              |> int64
                          UploadId = uploadId
                          ChunkSize = Config.CHUNK_SIZE
                          ChunkCount =
                              Redis
                                  .redis
                                  .HashGet(mpKey, RedisValue("chunkCount"))
                                  .ToString()
                              |> int
                          ChunkExists = chunks }

                    return! okResp "OK" ret next ctx
        }

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

             route "/user/signup" >=> User.UserRegister
             route "/user/signin" >=> User.UserLogin
             route "/user/signout" >=> User.UserLogout
             route "/user/info"
             >=> jwtAuthorized
             >=> User.UserInfoHandler

             route "/file/upload"
             >=> choose [ POST
                          >=> jwtAuthorized
                          >=> Upload.FileUploadHandler
                          RequestErrors.METHOD_NOT_ALLOWED id ]
             route "/file/meta"
             >=> jwtAuthorized
             >=> Upload.FileMetaHandler
             route "/file/recent"
             >=> jwtAuthorized
             >=> Upload.RecentFileHandler
             route "/file/download"
             >=> jwtAuthorized
             >=> Upload.FileDownloadHandler
             route "/file/update"
             >=> jwtAuthorized
             >=> Upload.FileUpdateHandler
             route "/file/delete"
             >=> jwtAuthorized
             >=> Upload.FileDeleteHandler

             route "/file/fastupload"
             >=> jwtAuthorized
             >=> MpUpload.TryFastUploadHandler
             route "/file/mpupload/init"
             >=> jwtAuthorized
             >=> MpUpload.InitMultipartUploadHandler
             route "/file/mpupload/uppart"
             >=> jwtAuthorized
             >=> MpUpload.UploadPartHandler
             route "/file/mpupload/complete"
             >=> jwtAuthorized
             >=> MpUpload.CompleteUploadPartHandler
             route "/file/mpupload/cancel"
             >=> jwtAuthorized
             >=> MpUpload.CancelUploadPartHandler
             route "/file/mpupload/status"
             >=> jwtAuthorized
             >=> MpUpload.MultipartUploadStatusHandler

             route "/error" >=> htmlFile "static/error.html"
             RequestErrors.notFound (text "Not Found") ]
