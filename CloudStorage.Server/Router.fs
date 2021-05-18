module CloudStorage.Server.Router

open System
open Giraffe

let routes : HttpHandler =
    choose [
        route "/" >=> htmlFile "static/index.html"
        route "/ping" >=> Successful.OK "pong!"
        route "/auth/ping" >=> Handler.jwtAuthorized >=> Successful.OK "pong!"
        route "/time" >=> warbler (fun _ -> text (DateTime.Now.ToString()))
        route "/test" >=> RequestErrors.UNAUTHORIZED "" "" ""
        
        route "/user/signup" >=> Handler.UserRegister
        route "/user/signin" >=> Handler.UserLogin
        route "/user/signout" >=> Handler.UserLogout
        route "/user/info" >=> Handler.jwtAuthorized >=> Handler.UserInfoHandler
        
        route "/file/upload" >=> choose [
            GET >=> RequestErrors.METHOD_NOT_ALLOWED ""  
            POST >=> Handler.jwtAuthorized >=> Handler.FileUploadHandler
        ]
        route "/file/meta" >=> Handler.jwtAuthorized >=> Handler.FileMetaHandler
        route "/file/recent" >=> Handler.jwtAuthorized >=> Handler.RecentFileHandler
        route "/file/download" >=> Handler.jwtAuthorized >=> Handler.FileDownloadHandler
        route "/file/update" >=> Handler.jwtAuthorized >=> Handler.FileUpdateHandler
        route "/file/delete" >=> Handler.jwtAuthorized >=> Handler.FileDeleteHandler
        
        route "/file/fastupload" >=> Handler.jwtAuthorized >=> Handler.TryFastUploadHandler
        route "/file/mpupload/init" >=> Handler.jwtAuthorized >=> Handler.InitMultipartUploadHandler
        route "/file/mpupload/uppart" >=> Handler.jwtAuthorized >=> Handler.UploadPartHandler
        route "/file/mpupload/complete" >=> Handler.jwtAuthorized >=> Handler.CompleteUploadPartHandler
        route "/file/mpupload/cancel" >=> Handler.jwtAuthorized >=> Handler.CancelUploadPartHandler
        route "/file/mpupload/status" >=> Handler.jwtAuthorized >=> Handler.MultipartUploadStatusHandler
            
        route "/error" >=> htmlFile "static/error.html"
           
        RequestErrors.notFound (text "Not Found")
    ]