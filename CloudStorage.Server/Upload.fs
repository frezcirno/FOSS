module CloudStorage.Server.Upload

open System
open System.IO
open System.Security.Claims
open Microsoft.AspNetCore.Http
open Giraffe
open CloudStorage.Common
open CloudStorage.Server


/// 用户上传文件
let FileUploadHandler (next: HttpFunc) (ctx: HttpContext) =
    let username = ctx.User.FindFirstValue ClaimTypes.Name

    if ctx.Request.Form.Files.Count = 1 then
        let file = ctx.Request.Form.Files.[0]

        let stream = file.OpenReadStream()
        let fileHash = Utils.StreamSha1 stream

        let saveResult =
            stream.Seek(0L, SeekOrigin.Begin) |> ignore
            Storage.SaveFile fileHash file.Name file.Length stream

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
            match Database.File.GetFileMetaByHash userFile.FileHash with
            | None -> ServerErrors.INTERNAL_ERROR "Sorry, this file is missing" next ctx
            | Some fileMeta ->
                let data = Storage.LoadFile fileMeta.FileLoc
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
