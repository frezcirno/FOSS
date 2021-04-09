module CloudStorage.Server.Router

open System
open Giraffe

let routes : HttpHandler =
    choose [
        route "/" >=> htmlView Views.index
        route "/ping" >=> Successful.OK "pong!"
        route "/time" >=> warbler (fun _ -> text (DateTime.Now.ToString()))
        route "/nothing" >=> setBody (System.Text.Encoding.ASCII.GetBytes "Hello!")
        
        route "/file/upload" >=> choose [
            GET >=> htmlView Views.upload
            POST >=> Handler.FileUploadHandler
        ]
        route "/file/upload/suc" >=> Successful.OK "Upload finished!"
        route "/file/meta" >=> Handler.NeedToken >=> Handler.FileMetaHandler
        route "/file/query" >=> Handler.NeedToken >=> Handler.FileQueryHandler
        route "/file/download" >=> Handler.NeedToken >=> Handler.FileDownloadHandler
        route "/file/update" >=> Handler.NeedToken >=> Handler.FileUpdateHandler
        route "/file/delete" >=> Handler.NeedToken >=> Handler.FileDeleteHandler
        
        route "/file/fastupload" >=> Handler.NeedToken >=> Handler.TryFastUploadHandler
        
        route "/user/signup" >=> Handler.UserSignupHandler
        route "/user/signin" >=> Handler.UserSigninHandler
        route "/user/info" >=> Handler.NeedToken >=> Handler.UserInfoHandler
  
        route "/file/mpupload/init" >=> Handler.InitMultipartUploadHandler
        route "/file/mpupload/uppart" >=> Handler.UploadPartHandler
        route "/file/mpupload/complete" >=> Handler.CompleteUploadPartHandler
        route "/file/mpupload/cancel" >=> Handler.CancelUploadPartHandler
        route "/file/mpupload/status" >=> Handler.MultipartUploadStatusHandler
            
        RequestErrors.notFound (text "Not Found")
    ]