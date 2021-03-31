module CloudStorage.Server.Router

open System
open Giraffe

let routes : HttpHandler =
    choose [
        route "/" >=> htmlView Views.index
        route "/ping" >=> Successful.ok (text "pong!")
        route "/time" >=> warbler (fun _ -> text <| DateTime.Now.ToString())
        
        route "/file/upload" >=> Handler.CheckToken >=> choose [
            GET >=> htmlView Views.upload
            POST >=> Handler.FileUploadHandler
        ]
        route "/file/upload/suc" >=> Successful.ok (text "Upload finished!")
        route "/file/meta" >=> Handler.CheckToken >=> Handler.FileMetaHandler
        route "/file/query" >=> Handler.CheckToken >=> Handler.FileQueryHandler
        route "/file/download" >=> Handler.CheckToken >=> Handler.FileDownloadHandler
        route "/file/update" >=> Handler.CheckToken >=> POST >=> Handler.FileUpdateHandler
        route "/file/delete" >=> Handler.CheckToken >=> Handler.FileDeleteHandler
        
        route "/user/signup" >=> choose [
            GET >=> htmlView Views.signup
            POST >=> Handler.UserSignupHandler
        ]
        route "/user/signin" >=> choose [
            GET >=> htmlView Views.signup
            POST >=> Handler.UserSigninHandler
        ]
        route "/user/info" >=> Handler.CheckToken >=> Handler.UserInfoHandler

        RequestErrors.notFound (text "Not Found")
    ]