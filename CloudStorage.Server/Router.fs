module CloudStorage.Server.Router

open System
open Giraffe

let routes : HttpHandler =
    choose [
        route "/" >=> htmlView Views.index
        route "/ping" >=> Successful.ok (text "pong!")
        route "/time" >=> warbler (fun _ -> text <| DateTime.Now.ToString())
        
        route "/file/upload" >=> choose [
            GET >=> htmlView Views.upload
            POST >=> Handlers.UploadHandler 
        ]
        route "/file/upload/suc" >=> Successful.ok (text "Upload finished!")
        route "/file/meta" >=> Handlers.FileMetaHandler
        route "/file/query" >=> Handlers.FileQueryHandler
        route "/file/download" >=> Handlers.FileDownloadHandler
        route "/file/update" >=> POST >=> Handlers.FileMetaUpdateHandler
        route "/file/delete" >=> Handlers.FileDeleteHandler
        
        RequestErrors.notFound (text "Not Found")
    ]