module CloudStorage.Storage.ApiServer.Objects

open System.IO
open Giraffe
open Microsoft.AspNetCore.Http


let getStream (objectName: string) : Stream =
    let server = Locate.Locate objectName

    if server = null then
        null
    else
        ObjectStream.GetStream server objectName

let getHandler (objectName: string) =
    let stream = getStream objectName
    streamData true stream None None

let putStream (objectName: string) (data: Stream) =
    let server = Heartbeat.ChooseRandomDataServer()

    if server = "" then
        null
    else
        ObjectStream.PutStream server objectName data


let storeObject (objectName: string) (data: Stream) = putStream objectName data

let putHandler (objectName: string) (next: HttpFunc) (ctx: HttpContext) =
    let body = ctx.Request.Body
    storeObject objectName body |> ignore
    Successful.ok id next ctx


let Handler (objectName: string) =
    choose [ GET >=> getHandler objectName
             PUT >=> putHandler objectName ]
