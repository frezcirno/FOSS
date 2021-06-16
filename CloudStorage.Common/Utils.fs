module CloudStorage.Common.Utils

open System.IO
open System.Security.Cryptography

///
/// Functional
///
let apply f x = f x

let apply2 x y z = x y z

let wrap x = fun () -> x

let skip _ = id

let skip2 _ = skip

let skip3 _ = skip2

let flip (f: 'a -> 'b -> 'c) (x: 'b) (y: 'a) : 'c = f y x

let flip' (f: 'a * 'b -> 'c) (xy: 'b * 'a) = f (snd xy, fst xy)

let firstOrNone =
    function
    | [] -> None
    | x :: _ -> Some x



///
/// String and Bytes
///
let ToHexString : (byte [] -> string) =
    Array.fold (fun state x -> state + sprintf "%02X" x) ""

let StringHash (hash: HashAlgorithm) (str: string) =
    System.Text.Encoding.ASCII.GetBytes(str)
    |> hash.ComputeHash
    |> ToHexString

let StreamHash (hash: HashAlgorithm) (stream: Stream) = hash.ComputeHash(stream) |> ToHexString

let StringMd5 : (string -> string) =
    StringHash(new MD5CryptoServiceProvider())

let StringSha1 : (string -> string) =
    StringHash(new SHA1CryptoServiceProvider())

let StreamSha1 : (Stream -> string) =
    StreamHash(new SHA1CryptoServiceProvider())

let BytesSha1 (bytes: byte []) : string =
    let sha1 = new SHA1CryptoServiceProvider()
    sha1.ComputeHash(bytes) |> ToHexString

///
/// Http
///
let ResponseBrief (code: int) (msg: string) = {| Code = code; Msg = msg |}

let Response (code: int) (msg: string) (data: obj) =
    {| Code = code
       Msg = msg
       Data = data |}

///
/// Configuration
///
