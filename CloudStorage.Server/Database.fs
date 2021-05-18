module CloudStorage.Server.Database

open System
open Giraffe
open MySqlConnector
open Dapper

let conn =
    new MySqlConnection(Config.Data.datasource)

module File =
    type FileMeta =
        { FileHash: String
          FileName: String
          FileSize: Int64
          FileLoc: String
          CreateAt: DateTime }

    /// 向文件表中插入一条新记录
    let CreateFileMeta (file_hash: string) (file_name: string) (file_size: Int64) (file_loc: string) : bool =
        let sql =
            "INSERT INTO tbl_file (file_hash, file_name, file_size, file_loc, status) "
            + "VALUES (@file_hash, @file_name, @file_size, @file_loc, @status)"

        let param =
            {| file_hash = file_hash
               file_name = file_name
               file_size = file_size
               file_loc = file_loc
               status = 1 |}

        conn.Execute(sql, param) = 1

    /// 更新文件元信息
    let UpdateFileMeta (file_hash: string) (file_name: string) (file_size: Int64) (file_loc: string) : bool =
        let sql =
            "UPDATE tbl_file SET file_name = @file_name, file_size = @file_size, file_loc = @file_loc "
            + "WHERE file_hash = @file_hash"

        let param =
            {| file_hash = file_hash
               file_name = file_name
               file_size = file_size
               file_loc = file_loc |}

        conn.Execute(sql, param) = 1

    /// 更新文件元信息
    let UpdateFileMetaByFileMeta (fileMeta: FileMeta) : bool =
        let sql =
            "UPDATE tbl_file SET file_name = @file_name, file_size = @file_size, file_loc = @file_loc "
            + "WHERE file_hash = @file_hash"

        let param =
            {| file_hash = fileMeta.FileHash
               file_name = fileMeta.FileName
               file_size = fileMeta.FileSize
               file_loc = fileMeta.FileLoc |}

        conn.Execute(sql, param) = 1

    /// 删除文件信息
    let DeleteFileMeta (file_hash: string) =
        let sql =
            "DELETE FROM tbl_file WHERE file_hash = @file_hash"

        let cmd = new MySqlCommand(sql, conn)

        cmd.Parameters.AddWithValue("@file_hash", file_hash)
        |> ignore

        cmd.ExecuteNonQuery() >= 1

    /// 返回文件信息
    let GetFileMetaByHash (file_hash: string) =
        let sql =
            "SELECT file_hash as FileHash, file_name as FileName, file_size as FileSize, file_loc as FileLoc, create_at as CreateAt FROM tbl_file "
            + "WHERE file_hash = @file_hash AND status = 1 LIMIT 1"

        conn.Query<FileMeta>(sql, {| file_hash = file_hash |})
        |> List.ofSeq
        |> Util.firstOrNone

    /// 存在文件哈希
    let FileHashExists (file_hash: string) =
        match GetFileMetaByHash file_hash with
        | Some _ -> true
        | _ -> false

    /// 查询所有文件
    let GetLatestFileMetas (limit: int) =
        let sql =
            "SELECT file_hash as FileHash, file_name as FileName, file_size as FileSize, file_loc as FileLoc, create_at as CreateAt FROM tbl_file "
            + "ORDER BY create_at DESC LIMIT @limit"

        conn.Query<FileMeta>(sql, {| limit = limit |})
        |> Seq.toList

module User =
    type User =
        { Username: string
          Email: string
          Phone: string
          SignupAt: DateTime
          LastActiveAt: DateTime
          Status: int }

    /// 用户注册
    let UserRegister (username: string) (enc_pass: string) : bool =
        let sql =
            "INSERT INTO tbl_user (user_name, user_pwd) values (@user_name, @user_pwd)"

        let param =
            {| user_name = username
               user_pwd = enc_pass |}

        conn.Execute(sql, param) = 1

    /// 用户登录
    let UserLogin (username: string) (enc_pass: string) : bool =
        let sql =
            "SELECT count(*) FROM tbl_user WHERE user_name = @user_name and user_pwd = @user_pwd and status = 1"

        let param =
            {| user_name = username
               user_pwd = enc_pass |}

        Convert.ToInt32(conn.ExecuteScalar(sql, param)) = 1

    /// 查询用户信息
    let GetUserByUsername (username: string) =
        let sql =
            "SELECT user_name as UserName, email as Email, phone as Phone, signup_at as SignupAt, last_active as LastActiveAt, status FROM tbl_user WHERE user_name = @user_name"

        let param = {| user_name = username |}

        conn.Query<User>(sql, param)
        |> List.ofSeq
        |> Util.firstOrNone

    ///
    let CheckUserToken username token =
        let sql =
            "SELECT count(*) FROM tbl_user_token WHERE user_name = @user_name AND user_token = @user_token"

        let param =
            {| user_name = username
               user_token = token |}

        conn.Query(sql, param)
        |> List.ofSeq
        |> function
        | [] -> false
        | _ -> true

module UserFile =
    type UserFile =
        { UserName: string
          FileHash: string
          FileName: string
          UploadAt: DateTime
          LastActive: DateTime }

    /// 更新用户文件表
    let CreateUserFile (user_name: string) (file_hash: string) (file_name: string) : bool =
        let sql =
            "INSERT INTO tbl_user_file (user_name, file_hash, file_name, upload_at) "
            + "VALUES (@user_name, @file_hash, @file_name, @upload_at)"

        let param =
            {| user_name = user_name
               file_hash = file_hash
               file_name = file_name
               upload_at = DateTime.Now |}

        conn.Execute(sql, param) = 1

    /// 查询用户是否拥有某个文件
    let IsUserHaveFile (user_name: string) (file_name: string) : bool =
        let sql =
            "SELECT count(*) FROM tbl_user_file "
            + "WHERE user_name = @user_name AND file_name = @file_name"

        let param =
            {| user_name = user_name
               file_name = file_name |}

        Convert.ToInt32(conn.ExecuteScalar(sql, param)) = 1

    /// 用户文件信息查询
    let GetUserFileByFileName (username: string) (fileName: string) =
        let sql =
            "SELECT user_name as UserName, file_hash as FileHash, file_name as FileName, upload_at as UploadAt, last_active as LastActive FROM tbl_user_file "
            + "WHERE user_name = @user_name AND file_name = @file_name AND status = 1 LIMIT 1"

        conn.Query<UserFile>(
            sql,
            {| file_name = fileName
               user_name = username |}
        )
        |> List.ofSeq
        |> Util.firstOrNone

    /// 获取用户近期文件元信息列表
    let GetUserFiles (username: string) (page: int) (limit: int) =
        let sql =
            "SELECT user_name as UserName, file_hash as FileHash, file_name as FileName, upload_at as UploadAt, last_active as LastActive FROM tbl_user_file "
            + "WHERE user_name = @user_name ORDER BY upload_at DESC LIMIT @limit OFFSET @offset"

        let param =
            {| user_name = username
               limit = limit
               offset = page * limit |}

        conn.Query<UserFile>(sql, param) |> List.ofSeq

    /// 更新文件元信息
    let UpdateUserFileByUserFile (username: string) (fileName: string) (userFile: UserFile) : bool =
        let sql =
            "UPDATE tbl_user_file SET file_name = @new_name "
            + "WHERE user_name = @user_name AND file_name = @file_name"

        let param =
            {| user_name = username
               file_name = fileName
               new_name = userFile.FileName |}

        conn.Execute(sql, param) = 1

    /// 用户文件删除
    let DeleteUserFileByFileName (username: string) (fileName: string) =
        let sql =
            "DELETE FROM tbl_user_file WHERE user_name = @user_name AND file_name = @file_name"

        let cmd = new MySqlCommand(sql, conn)

        cmd.Parameters.AddWithValue("@user_name", username)
        |> ignore

        cmd.Parameters.AddWithValue("@file_name", fileName)
        |> ignore

        cmd.ExecuteNonQuery() >= 1
