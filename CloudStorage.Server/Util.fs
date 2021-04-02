module CloudStorage.Server.Util

open System
open System.IO
open System.Security.Cryptography

let private sha1 = HashAlgorithm.Create("sha1")
let private md5 = HashAlgorithm.Create("md5")
let private salt = "frezcirnoisthebest"
let private tokensalt = "frezcirnolikecoding"

let apply f x = f x
let apply2 x y z = x y z
let wrap x = fun () -> x
let ignore x = fun _ -> x
let skip _ = id
let skip2 _ = skip
let skip3 _ = skip2
let flip f x y = f y x

let private ByteToHex: (byte [] -> string) =
    Array.fold (fun state x -> state + sprintf "%02X" x) ""

let StringMd5: (string -> string) =
    System.Text.Encoding.ASCII.GetBytes
    >> md5.ComputeHash
    >> ByteToHex

let StreamSha1: (Stream -> string) = sha1.ComputeHash >> ByteToHex

let ByteSha1: (byte [] -> string) = sha1.ComputeHash >> ByteToHex

let StringSha1: (string -> string) =
    System.Text.Encoding.ASCII.GetBytes
    >> sha1.ComputeHash
    >> ByteToHex


let EncryptPasswd = flip (+) salt >> StringSha1

let GenToken username =
    let ts =
        DateTimeOffset(DateTime.Now)
            .ToUnixTimeSeconds()
            .ToString()

    let tokenPrefix = StringMd5(username + ts + tokensalt)
    tokenPrefix + ts.[0..7]

let IsTokenValid (username: string) (token: string) =
    token.Length = 40
    &&
    let ts = token.[32..39]
    let _token = StringMd5(username + ts + tokensalt)
    token.Equals _token
