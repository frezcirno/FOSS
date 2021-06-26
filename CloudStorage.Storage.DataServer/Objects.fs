module CloudStorage.Storage.DataServer.Objects

open System.IO
open CloudStorage.Common
open Giraffe
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

let PutObjectHandler (object_name: string) (next: HttpFunc) (ctx: HttpContext) =
    task {
        try
            let path =
                Path.Join [| Config.TEMP_PATH
                             object_name |]

            use f = File.OpenWrite path

            if not f.CanWrite then
                return! ServerErrors.internalError id next ctx
            else
                do! ctx.Request.Body.CopyToAsync f
                return! Successful.ok id next ctx
        with
        | :? BadHttpRequestException as ex ->
            System.Diagnostics.Debug.WriteLine (sprintf  $"%s{ex.StackTrace}")
            return! RequestErrors.BAD_REQUEST "file size too large" next ctx
        | :? IOException as ex ->
            System.Diagnostics.Debug.WriteLine (sprintf  $"%s{ex.StackTrace}")
            return! RequestErrors.notFound id next ctx
        | ex ->
            System.Diagnostics.Debug.WriteLine (sprintf  $"%s{ex.StackTrace}")
            return! ServerErrors.internalError id next ctx
    }


let GetObjectHandler (object_name: string) (next: HttpFunc) (ctx: HttpContext) =
    try
        let path =
            Path.Join [| Config.TEMP_PATH
                         object_name |]

        let f = File.OpenRead path

        if not f.CanRead then
            ServerErrors.internalError id next ctx
        else
            streamData true f None None next ctx
    with
    | :? IOException as ex ->
        System.Diagnostics.Debug.WriteLine (sprintf  $"%s{ex.StackTrace}")
        RequestErrors.notFound id next ctx
    | ex ->
        System.Diagnostics.Debug.WriteLine (sprintf  $"%s{ex.StackTrace}")
        ServerErrors.internalError id next ctx



let Handler (object_name: string) =
    choose [ GET >=> GetObjectHandler(object_name)
             PUT >=> PutObjectHandler(object_name) ]
