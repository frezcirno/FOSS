module CloudStorage.Server.Util

open System
open System.Buffers.Text
open System.IO
open System.Security.Cryptography
open System.Text

let apply f x = f x

let apply2 x y z = x y z

let wrap x = fun () -> x

let ignore x = fun _ -> x

let skip _ = id

let skip2 _ = skip

let skip3 _ = skip2

let flip f x y = f y x

let firstOrNone =
    function
    | [] -> None
    | x :: _ -> Some x


let private sha1 = new SHA1CryptoServiceProvider()

let private md5 = new MD5CryptoServiceProvider()

let private ByteToHex : (byte [] -> string) =
    Array.fold (fun state x -> state + sprintf "%02X" x) ""

let StringMd5 : (string -> string) =
    System.Text.Encoding.ASCII.GetBytes
    >> md5.ComputeHash
    >> ByteToHex

let StreamSha1 : (Stream -> string) = sha1.ComputeHash >> ByteToHex

let ByteSha1 : (byte [] -> string) = sha1.ComputeHash >> ByteToHex

let StringSha1 : (string -> string) =
    System.Text.Encoding.ASCII.GetBytes
    >> sha1.ComputeHash
    >> ByteToHex


let EncryptPasswd =
    flip (+) Config.Security.Salt >> StringSha1
