module CloudStorage.Storage.ApiServer.Objects

open System.IO
open System.Threading.Tasks
open Giraffe
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

let getStream (objectName: string) : Stream =
    let server = Locate.Locate objectName

    if server = "" then
        null
    else
        ObjectStream.GetStream server objectName

let getHandler (objectName: string) =
    let stream = getStream objectName
    if stream = null then
        RequestErrors.notFound id
    else
        streamData true stream None None

let putStream (objectName: string) (data: Stream) : Task<bool> =
    task {
        let server = Heartbeat.ChooseRandomDataServer()
        System.Diagnostics.Debug.WriteLine (sprintf "Put stream use %s" server)
        if server = "" then
            return false
        else
            return! ObjectStream.PutStream server objectName data
    }


let storeObject (objectName: string) (data: Stream) = putStream objectName data

let putHandler (objectName: string) (next: HttpFunc) (ctx: HttpContext) =
    task {
        let body = ctx.Request.Body
        let! res = storeObject objectName body
        if res then
            return! Successful.ok id next ctx
        else
            return! ServerErrors.serviceUnavailable id next ctx
    }

let Handler (objectName: string) =
    choose [ GET >=> getHandler objectName
             PUT >=> putHandler objectName ]
