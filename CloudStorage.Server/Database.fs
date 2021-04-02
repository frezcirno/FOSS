module CloudStorage.Server.Database

open System
open System.Data
open Giraffe
open Giraffe.ComputationExpressions
open MySqlConnector
open Dapper
//open Dapper.FSharp
//open Dapper.FSharp.MySQL

type Tbl_file =
    { id : int
      file_sha1 : string
      file_name : string
      file_size : Int64
      file_loc : string
      create_at : DateTime
      update_at : DateTime
      status : int }

type Tbl_user =
    { id : int
      user_name : string
      user_pwd : string
      email : string
      email_validated : bool
      phone : string
      phone_validated : bool
      signup_at : DateTime
      last_active : DateTime
      profile : string
      status : int }

type Tbl_user_file =
    { id : int
      user_name : string
      file_sha1 : string
      file_name : string
      upload_at : DateTime
      last_update : DateTime
      status : int }

type Tbl_user_token =
    { id : int
      user_name : string
      user_token : string }
    
let firstOrNone = function
    | [] -> None
    | x :: _ -> Some x 

let conn = new MySqlConnection "Server=localhost;Database=test;User=root;Password=root"


/// <summary>
/// 向文件表中插入一条新记录
/// </summary>
/// <param name="filehash"></param>
/// <param name="filename"></param>
/// <param name="filesize"></param>
/// <param name="fileloc"></param>
/// <returns>是否成功</returns>
let CreateFileMeta (filehash : string)
                   (filename : string)
                   (filesize : Int64)
                   (fileloc : string) : bool =
    let sql = "INSERT IGNORE INTO tbl_file (file_sha1, file_name, file_size, file_loc, status) "
            + "VALUES (@file_sha1, @file_name, @file_size, @file_loc, @status)"
    let param = {|
        file_sha1 = filehash
        file_name = filename
        file_size = filesize
        file_loc = fileloc
        status = 1
    |}
    conn.Execute (sql, param) = 1
    
/// 更新文件元信息
let UpdateFileMeta (filehash : string)
                   (filename : string)
                   (filesize : Int64)
                   (fileloc : string) : bool =
    let sql = "UPDATE tbl_file SET file_name = @file_name, file_size = @file_size, file_loc = @file_loc "
            + "WHERE file_sha1 = @file_sha1"
    let param = {|
        file_sha1 = filehash
        file_name = filename
        file_size = filesize
        file_loc = fileloc
    |}
    conn.Execute (sql, param) = 1

let DeleteFileMeta (fsha1: string) =
    let sql = "DELETE FROM tbl_file WHERE file_sha1 = @file_sha1"
    let cmd = new MySqlCommand (sql, conn)
    cmd.Parameters.AddWithValue("@file_sha1", fsha1) |> ignore
    cmd.ExecuteNonQuery()

[<CLIMutable>]
type FileMetaObj = { file_sha1 : string
                     file_name : string
                     file_size : Int64
                     file_loc : string
                     create_at : DateTime }

/// 返回文件信息
let GetFileMetaByHash (fsha1: string) =
    let sql = "SELECT file_sha1, file_name, file_size, file_loc, create_at FROM tbl_file "
            + "WHERE file_sha1 = @file_sha1 AND status = 1 LIMIT 1"
    conn.Query<FileMetaObj>(sql, {| file_sha1 = fsha1 |})
    |> Seq.map (fun (x : FileMetaObj) -> { FileMeta.FileSha1 = x.file_sha1
                                           FileName = x.file_name
                                           FileSize = x.file_size
                                           Location = x.file_loc
                                           UploadAt = x.create_at.ToIsoString() } ) 
    |> List.ofSeq
    |> firstOrNone


let GetLatestFileMetas limit =
    let sql = "SELECT file_sha1, file_name, file_size, file_loc, create_at FROM tbl_file "
            + "ORDER BY create_at DESC LIMIT @limit"
    conn.Query<FileMeta>(sql, {| limit = limit |})
    |> Seq.toList


/// 用户注册
let UserSignup (username : string)
               (enc_pass : string) : bool =
    let sql = "INSERT IGNORE INTO tbl_user (user_name, user_pwd) values (@user_name, @user_pwd)"
    let param = {|
        user_name = username
        user_pwd = enc_pass
    |}
    conn.Execute (sql, param) = 1

/// 用户登录
let UserSignin (username : string)
               (enc_pass : string) : bool =
    let sql = "SELECT count(*) FROM tbl_user WHERE user_name = @user_name and user_pwd = @user_pwd and status = 1"
    let param = {|
        user_name = username
        user_pwd = enc_pass
    |}
    Convert.ToInt32 (conn.ExecuteScalar (sql, param)) = 1
    
/// 刷新用户token
let UserUpdateToken (username : string)
                    (usertoken : string) : bool =
    let sql = "REPLACE INTO tbl_user_token (user_name, user_token) VALUES (@user_name, @user_token)"
    let param = {|
        user_name = username
        user_token = usertoken
    |}
    conn.Execute (sql, param) = 1
    
type UserObj = {
    user_name : string
    email : string
    phone : string
    signup_at : DateTime
    last_active : DateTime
    status : int
}
    
/// 查询用户信息
let GetUserByUsername (username : string) =
    let sql = "SELECT user_name, signup_at FROM tbl_user WHERE user_name = @user_name"
    let param = {|
        user_name = username
    |}
    conn.Query<UserObj>(sql, param)
    |> Seq.map (fun x -> { User.Username = x.user_name
                           Email = x.email
                           Phone = x.phone
                           SignupAt = x.signup_at.ToIsoString()
                           LastActiveAt = x.last_active.ToIsoString()
                           Status = x.status })
    |> List.ofSeq
    |> firstOrNone
    
    
let CheckUserToken username token =
    let sql = "SELECT count(*) FROM tbl_user_token WHERE user_name = @user_name AND user_token = @user_token"
    let param = {|
        user_name = username
        user_token = token
    |}
    conn.Query(sql, param)
    |> List.ofSeq
    |> function
        | [] -> false
        | _ -> true
        
        
/// 更新用户文件表
let CreateUserFile (username : string)
                   (filehash : string)
                   (filename : string) : bool =
    let sql = "INSERT IGNORE INTO tbl_user_file (user_name, file_sha1, file_name, upload_at) "
            + "VALUES (@user_name, @file_sha1, @file_name, @upload_at)"
    let param = {|
        user_name = username
        file_sha1 = filehash
        file_name = filename
        upload_at = DateTime.Now
    |}
    conn.Execute(sql, param) = 1



[<CLIMutable>]
type UserFileObj = { file_sha1 : string
                     file_name : string
                     file_size : Int64
                     upload_at : DateTime
                     last_update : DateTime }

/// 查询用户是否拥有某个文件
let IsUserHaveFile (username : string)
                   (fsha1 : string) =
    let sql = "SELECT count(*) FROM tbl_user_file "
            + "WHERE user_name = @user_name AND file_sha1 = @file_sha1"
    let param = {|
        user_name = username
        file_sha1 = fsha1
    |}
    conn.Execute(sql, param) = 1

/// 获取用户近期文件元信息列表
let GetLatestUserFileMetas username limit =
    let sql = "SELECT file_sha1, file_name, file_size, upload_at, last_update FROM tbl_user_file "
            + "WHERE user_name = @user_name LIMIT @limit"
    let param = {|
        user_name = username
        limit = limit
    |}
    conn.Query<UserFileObj>(sql, param)
    |> Seq.map (fun x -> { UserFile.FileHash = x.file_sha1
                           UserName = username
                           FileName = x.file_name
                           FileSize = x.file_size
                           UploadAt = x.upload_at.ToIsoString()
                           LastUpdated = x.last_update.ToIsoString() })
    |> List.ofSeq