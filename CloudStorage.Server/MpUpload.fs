module CloudStorage.Server.MpUpload

open System
open System.IO
open System.Security.Claims
open CloudStorage.Common
open CloudStorage.Server
open Giraffe
open FSharp.Control.Tasks
open Microsoft.AspNetCore.Http
open StackExchange.Redis

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
            /// 查询文件是否存在
            if not (Database.File.FileHashExists initBlock.fileHash) then
                return! jsonResp -1 "秒传失败，请访问普通上传接口" null next ctx
            /// 存在则尝试秒传
            else if Database.UserFile.CreateUserFile username initBlock.fileHash initBlock.fileName initBlock.fileSize then
                return! okResp "OK" null next ctx
            else
                return! ServerErrors.SERVICE_UNAVAILABLE "秒传服务暂不可用，请访问普通上传接口" next ctx
    }

[<CLIMutable>]
type MultipartUploadBlock = { fileHash: string; fileSize: int64 }

[<CLIMutable>]
type MultipartInfo =
    { UploadKey: string
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
            let hashKey =
                RedisKey(Config.HASH_KEY_PREFIX + args.fileHash)

            let uploadKey =
                /// 如果 HASH_KEY_PREFIX + hash -> uploadKey 存在，说明是断点续传
                match Redis.redis.KeyExists hashKey with
                /// 不存在则创建一个新的
                | false -> Utils.StringSha1(username + args.fileHash + Guid().ToString())
                | true -> Redis.redis.StringGet(hashKey).ToString()

            /// 存在则获取chunk info
            let chunks =
                Redis.redis.ListRange(RedisKey(Config.CHUNK_KEY_PREFIX + uploadKey))
                |> Array.map int

            let ret =
                { UploadKey = uploadKey
                  ChunkSize = Config.CHUNK_SIZE
                  ChunkCount =
                      float args.fileSize / float Config.CHUNK_SIZE
                      |> ceil
                      |> int
                  ChunkExists = chunks }

            ///
            /// 如果Redis中不存在uploadId Key, 说明是第一次上传
            /// 初始化断点信息到redis
            ///
            let mpKey =
                RedisKey(Config.UPLOAD_INFO_KEY_PREFIX + ret.UploadKey)

            if not (Redis.redis.KeyExists mpKey) then

                Redis.redis.HashSet(mpKey, RedisValue("username"), RedisValue(username))
                |> ignore

                Redis.redis.HashSet(mpKey, RedisValue("chunkCount"), RedisValue(string ret.ChunkCount))
                |> ignore

                Redis.redis.HashSet(mpKey, RedisValue("fileHash"), RedisValue(args.fileHash))
                |> ignore

                Redis.redis.HashSet(mpKey, RedisValue("fileSize"), RedisValue(string args.fileSize))
                |> ignore

                Redis.redis.KeyExpire(mpKey, TimeSpan.FromHours(8.0))
                |> ignore

                Redis.redis.StringSet(hashKey, RedisValue(ret.UploadKey), TimeSpan.FromHours(12.0))
                |> ignore

            return! okResp "OK" ret next ctx
    }

[<CLIMutable>]
type UploadPartBlock = { uploadKey: string; index: int }

///
/// 上传一个分片
///
let UploadPartHandler (next: HttpFunc) (ctx: HttpContext) =
    task {
        let username = ctx.User.FindFirstValue ClaimTypes.Name

        match ctx.TryBindQueryString<UploadPartBlock>() with
        | Error msg -> return! ArgumentError msg next ctx
        | Ok partInfo ->
            let mpKey =
                RedisKey(Config.UPLOAD_INFO_KEY_PREFIX + partInfo.uploadKey)

            /// 检查 uploadKey 是否存在
            if not (Redis.redis.KeyExists mpKey) then
                return! RequestErrors.notFound id next ctx
            else
                /// 检查用户是否正确
                let realuser =
                    Redis.redis.HashGet(mpKey, RedisValue("username"))

                if realuser.ToString() <> username then
                    return! RequestErrors.FORBIDDEN "Not your file!" next ctx
                else
                    let data = ctx.Request.Body

                    /// 将分片保存到 TEMP/upId/index
                    let fPath =
                        Path.Join [| Config.TEMP_FILE_PATH
                                     partInfo.uploadKey
                                     string partInfo.index |]

                    Directory.GetParent(fPath).Create()
                    use chunk = File.Create fPath
                    do! data.CopyToAsync chunk

                    ///
                    /// 每一个分片完成就在 CHUNK_KEY_PREFIX + uploadKey -> [  ] 中添加一项 index
                    ///
                    let chunkKey =
                        RedisKey(Config.CHUNK_KEY_PREFIX + partInfo.uploadKey)

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
    { uploadKey: string
      fileHash: string
      fileSize: int64
      fileName: string }

let __CompleteUploadPart (username: string) (args: CompletePartBlock) =
    let totalCount =
        Redis.redis.HashGet(RedisKey(Config.UPLOAD_INFO_KEY_PREFIX + args.uploadKey), RedisValue("chunkCount"))
        |> int

    let uploadCount =
        Redis.redis.ListLength(RedisKey(Config.CHUNK_KEY_PREFIX + args.uploadKey))
        |> int

    if totalCount <> uploadCount then
        jsonResp -2 "invalid request" null
    else

        let tempFolder =
            Path.Join [| Config.TEMP_FILE_PATH
                         args.uploadKey |]

        use mergeStream = MergeParts tempFolder totalCount

        if not (Storage.SaveFile args.fileHash args.fileName args.fileSize mergeStream) then
            ServerErrors.SERVICE_UNAVAILABLE "SaveFile"
        elif not (Database.UserFile.CreateUserFile username args.fileHash args.fileName args.fileSize) then
            ServerErrors.SERVICE_UNAVAILABLE "CreateUserFile"
        else
            /// 清理Redis
            let mpKey =
                RedisKey(Config.UPLOAD_INFO_KEY_PREFIX + args.uploadKey)

            let chunkKey =
                RedisKey(Config.CHUNK_KEY_PREFIX + args.uploadKey)

            let hashKey =
                RedisKey(Config.HASH_KEY_PREFIX + args.fileHash)

            if Redis.redis.KeyExists mpKey then

                Directory.Delete(tempFolder, true)
                Redis.redis.KeyDelete(hashKey) |> ignore
                Redis.redis.KeyDelete(mpKey) |> ignore
                Redis.redis.KeyDelete(chunkKey) |> ignore

            okResp "OK" null

///
/// 分片上传完成接口
///
let CompleteUploadPartHandler (next: HttpFunc) (ctx: HttpContext) =
    task {
        let username = ctx.User.FindFirstValue ClaimTypes.Name

        match! ctx.TryBindFormAsync<CompletePartBlock>() with
        | Error msg -> return! ArgumentError msg next ctx
        | Ok args ->
            let mpKey =
                RedisKey(Config.UPLOAD_INFO_KEY_PREFIX + args.uploadKey)

            if not (Redis.redis.KeyExists mpKey) then
                return! RequestErrors.notFound id next ctx
            else
                /// 检查用户是否正确
                let realuser =
                    Redis.redis.HashGet(mpKey, RedisValue("username"))

                if realuser.ToString() <> username then
                    return! RequestErrors.FORBIDDEN "Not your file!" next ctx
                else
                    /// 防止多次提交
                    let dupKey =
                        RedisKey(
                            Config.HASH_KEY_PREFIX
                            + args.fileHash
                            + "_processing"
                        )

                    if Redis.redis.KeyExists dupKey then
                        return! RequestErrors.tooManyRequests id next ctx
                    else
                        Redis.redis.StringSet(dupKey, RedisValue("1"))
                        |> ignore

                        let res = __CompleteUploadPart username args
                        Redis.redis.KeyDelete dupKey |> ignore
                        return! res next ctx
    }

///
/// 取消分片上传接口
///
let CancelUploadPartHandler (next: HttpFunc) (ctx: HttpContext) =
    task {
        let username = ctx.User.FindFirstValue ClaimTypes.Name

        match ctx.GetFormValue "uploadKey" with
        | None -> return! ArgumentError "uploadKey" next ctx
        | Some uploadKey ->
            let mpKey =
                RedisKey(Config.UPLOAD_INFO_KEY_PREFIX + uploadKey)

            /// 检查 uploadKey 是否存在
            if not (Redis.redis.KeyExists mpKey) then
                return! RequestErrors.notFound id next ctx
            else

                /// 检查用户是否正确
                let realuser =
                    Redis.redis.HashGet(mpKey, RedisValue("username"))

                if realuser.ToString() <> username then
                    return! RequestErrors.FORBIDDEN "Not your file!" next ctx
                else
                    let fileHash =
                        Redis.redis.HashGet(mpKey, RedisValue("fileHash"))

                    let hashKey =
                        RedisKey(Config.HASH_KEY_PREFIX + fileHash.ToString())

                    let chunkKey =
                        RedisKey(Config.CHUNK_KEY_PREFIX + uploadKey)

                    let tempFolder =
                        Path.Join [| Config.TEMP_FILE_PATH
                                     uploadKey |]

                    Directory.Delete(tempFolder, true)
                    Redis.redis.KeyDelete(hashKey) |> ignore
                    Redis.redis.KeyDelete(mpKey) |> ignore
                    Redis.redis.KeyDelete(chunkKey) |> ignore

                    return! okResp "OK" "Success delete" next ctx
    }

///
/// 查看分片上传状态接口
///
let MultipartUploadStatusHandler (next: HttpFunc) (ctx: HttpContext) =
    task {
        let username = ctx.User.FindFirstValue ClaimTypes.Name

        match ctx.GetFormValue "uploadKey" with
        | None -> return! ArgumentError "uploadKey is needed" next ctx
        | Some uploadKey ->
            let mpKey =
                RedisKey(Config.UPLOAD_INFO_KEY_PREFIX + uploadKey)

            if not (Redis.redis.KeyExists mpKey) then
                return! RequestErrors.notFound id next ctx
            else
                /// 检查用户是否正确
                let realuser =
                    Redis.redis.HashGet(mpKey, RedisValue("username"))

                if realuser.ToString() <> username then
                    return! RequestErrors.FORBIDDEN "Not your file!" next ctx
                else
                    let chunks =
                        Redis.redis.ListRange(RedisKey(Config.CHUNK_KEY_PREFIX + uploadKey))
                        |> Array.map (fun x -> x.ToString() |> int)

                    let ret =
                        {| UploadKey = uploadKey
                           FileHash =
                               Redis
                                   .redis
                                   .HashGet(mpKey, RedisValue("fileHash"))
                                   .ToString()
                           FileSize =
                               Redis
                                   .redis
                                   .HashGet(mpKey, RedisValue("fileSize"))
                                   .ToString()
                               |> int64
                           ChunkSize = Config.CHUNK_SIZE
                           ChunkCount =
                               Redis
                                   .redis
                                   .HashGet(mpKey, RedisValue("chunkCount"))
                                   .ToString()
                               |> int
                           ChunkExists = chunks |}

                    return! okResp "OK" ret next ctx
    }
