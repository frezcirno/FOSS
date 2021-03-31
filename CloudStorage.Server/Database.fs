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

let firstOrNone = function
    | [] -> None
    | x :: _ -> Some x 

let conn = new MySqlConnection "Server=localhost;Database=test;User=root;Password=root"

let CreateFileMeta (fileMeta: FileMetaEntity) =
    let sql = "INSERT INTO tbl_file (file_sha1, file_name, file_size, file_loc) values (@file_sha1, @file_name, @file_size, @file_loc)"
    let param = {|
        file_sha1 = fileMeta.FileSha1
        file_name = fileMeta.FileName
        file_loc = fileMeta.Location
        file_size = fileMeta.FileSize
        create_at = DateTime.Parse fileMeta.UploadAt
    |}
    conn.Execute (sql, param)
    
let UpdateFileMeta (fileMeta: FileMetaEntity) =
    let sql = "UPDATE tbl_file SET file_name = @file_name, file_size = @file_size, file_loc = @file_loc WHERE file_sha1 = @file_sha1"
    let param = {|
        file_sha1 = fileMeta.FileSha1
        file_name = fileMeta.FileName
        file_loc = fileMeta.Location
        file_size = fileMeta.FileSize
    |}
    conn.Execute (sql, param)

let DeleteFileMeta (fsha1: string) =
    let sql = "DELETE FROM tbl_file WHERE file_sha1 = @file_sha1"
    let cmd = new MySqlCommand (sql, conn)
    cmd.Parameters.AddWithValue("@file_sha1", fsha1) |> ignore
    cmd.ExecuteNonQuery()

type FileMetaObj = { file_sha1 : string
                     file_name : string
                     file_size : Int64
                     file_loc : string
                     create_at : DateTime }

let GetFileMetaByHash (fsha1: string) =
    let sql = "SELECT file_sha1, file_name, file_size, file_loc, create_at FROM tbl_file WHERE file_sha1 = @file_sha1"
    conn.Query<FileMetaObj>(sql, {| file_sha1 = fsha1 |})
    |> List.ofSeq    
    |> firstOrNone

let GetLatestFileMetas count' =
    let sql = "SELECT file_sha1, file_name, file_size, file_loc, create_at FROM tbl_file ORDER BY create_at DESC LIMIT @count"
    conn.Query<FileMetaEntity>(sql, {| count = count' |}) |> Seq.toList

let UserSignup username enc_pass =
    let sql = "INSERT INTO tbl_user (user_name, user_pwd) values (@user_name, @user_pwd)"
    let param = {|
        user_name = username
        user_pwd = enc_pass
    |}
    conn.Execute (sql, param) 

let UserSignin username enc_pass =
    let sql = "SELECT count(*) FROM tbl_user WHERE user_name = @user_name and user_pwd = @user_pwd and status = 0"
    let param = {|
        user_name = username
        user_pwd = enc_pass
    |}
    Convert.ToInt32 (conn.ExecuteScalar (sql, param)) > 0
    
let UserUpdateToken username usertoken =
    let sql = "INSERT INTO tbl_user_token (user_name, user_token) VALUES (@user_name, @user_token)"
    let param = {|
        user_name = username
        user_token = usertoken
    |}
    conn.Execute (sql, param) 
    
let GetUserByUsername username =
    let sql = "SELECT user_name, email, phone, FROM tbl_user WHERE user_name = @user_name"
    let param = {|
        user_name = username
    |}
    conn.Query<Tbl_user>(sql, param)
    |> List.ofSeq
    |> firstOrNone
    