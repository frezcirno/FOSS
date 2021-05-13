module CloudStorage.Server.Router

open System
open Giraffe

let routes : HttpHandler =
    choose [
        route "/" >=> htmlView Views.index
        route "/ping" >=> Successful.OK "pong!"
        route "/auth/ping" >=> Handler.cookieAuthorized >=> Successful.OK "pong!"
        route "/time" >=> warbler (fun _ -> text (DateTime.Now.ToString()))
        route "/test" >=> RequestErrors.UNAUTHORIZED "" "" ""
        
        route "/file/upload" >=> choose [
            GET >=> htmlView Views.upload
            POST >=> Handler.FileUploadHandler
        ]
        route "/file/upload/suc" >=> Successful.OK "Upload finished!"
        route "/file/meta" >=> Handler.cookieAuthorized >=> Handler.FileMetaHandler
        route "/file/query" >=> Handler.cookieAuthorized >=> Handler.FileQueryHandler
        route "/file/download" >=> Handler.cookieAuthorized >=> Handler.FileDownloadHandler
        route "/file/update" >=> Handler.cookieAuthorized >=> Handler.FileUpdateHandler
        route "/file/delete" >=> Handler.cookieAuthorized >=> Handler.FileDeleteHandler
        
        route "/file/fastupload" >=> Handler.cookieAuthorized >=> Handler.TryFastUploadHandler
        
        route "/user/signup" >=> choose [
            GET >=> htmlView Views.signup
            POST >=> Handler.UserRegister
        ]
        route "/user/signin" >=> choose [
            GET >=> htmlView Views.signin
            POST >=> Handler.UserLogin
        ]
        route "/user/info" >=> Handler.cookieAuthorized >=> Handler.UserInfoHandler
  
        route "/file/mpupload/init" >=> Handler.InitMultipartUploadHandler
        route "/file/mpupload/uppart" >=> Handler.UploadPartHandler
        route "/file/mpupload/complete" >=> Handler.CompleteUploadPartHandler
        route "/file/mpupload/cancel" >=> Handler.CancelUploadPartHandler
        route "/file/mpupload/status" >=> Handler.MultipartUploadStatusHandler
            
        route "/error" >=> htmlView Views.error
           
        RequestErrors.notFound (text "Not Found")
    ]