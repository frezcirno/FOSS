open System.IO
open System.Text
open CloudStorage.Common
open CloudStorage.Server
open System
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Giraffe
open Microsoft.IdentityModel.Tokens

let routes : HttpHandler =
    choose [ route "/user/signup" >=> User.UserRegister
             route "/user/signin" >=> User.UserLogin
             //             route "/user/signout" >=> User.UserLogout
             route "/user/info"
             >=> jwtAuthorized
             >=> User.UserInfoHandler

             route "/file/upload"
             >=> choose [ POST
                          >=> jwtAuthorized
                          >=> Upload.FileUploadHandler
                          RequestErrors.methodNotAllowed id ]
             route "/file/meta"
             >=> jwtAuthorized
             >=> Upload.FileMetaHandler
             route "/file/recent"
             >=> jwtAuthorized
             >=> Upload.RecentFileHandler
             route "/file/download"
             >=> jwtAuthorized
             >=> Upload.FileDownloadHandler
             route "/file/update"
             >=> jwtAuthorized
             >=> Upload.FileUpdateHandler
             route "/file/delete"
             >=> jwtAuthorized
             >=> Upload.FileDeleteHandler

             route "/file/fastupload"
             >=> jwtAuthorized
             >=> MpUpload.TryFastUploadHandler
             route "/file/mpupload/init"
             >=> jwtAuthorized
             >=> MpUpload.InitMultipartUploadHandler
             route "/file/mpupload/uppart"
             >=> jwtAuthorized
             >=> MpUpload.UploadPartHandler
             route "/file/mpupload/complete"
             >=> jwtAuthorized
             >=> MpUpload.CompleteUploadPartHandler
             route "/file/mpupload/cancel"
             >=> jwtAuthorized
             >=> MpUpload.CancelUploadPartHandler
             route "/file/mpupload/status"
             >=> jwtAuthorized
             >=> MpUpload.MultipartUploadStatusHandler

             RequestErrors.notFound (text "404 Not Found") ]



let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")

    clearResponse
    >=> ServerErrors.internalError (text ex.Message)


type Startup(configuration: IConfiguration) =
    member _.Configuration = configuration

    member this.ConfigureServices(services: IServiceCollection) =
        services.AddResponseCompression() |> ignore

        services.AddIdentity().AddRoles() |> ignore

        services
            .AddAuthentication(fun opt ->
                opt.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme
                opt.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme
                opt.DefaultSignInScheme <- JwtBearerDefaults.AuthenticationScheme
                opt.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme)
            //            .AddCookie(fun opt ->
//                opt.Cookie.Name <- "Cookies"
//                opt.LoginPath <- PathString "/user/signin"
//                opt.LogoutPath <- PathString "/user/signout"
//                opt.ExpireTimeSpan <- TimeSpan.FromHours(1.0))
            .AddJwtBearer(fun opt ->
                opt.Audience <- "api"
                opt.RequireHttpsMetadata <- false
                //                opt.TokenValidationParameters <- TokenValidationParameters()
                opt.TokenValidationParameters.IssuerSigningKey <-
                    SymmetricSecurityKey(Encoding.ASCII.GetBytes Config.Security.Secret)

                opt.TokenValidationParameters.ValidIssuer <- "http://7c00h.xyz/cloud"
                opt.SaveToken <- false)
        |> ignore

        services.AddGiraffe() |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member _.Configure (app: IApplicationBuilder) (env: IHostEnvironment) (loggerFactory: ILoggerFactory) =
        env.EnvironmentName <- Environments.Development

        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage() |> ignore
        else
            app.UseExceptionHandler "/error" |> ignore

        app
            .UseAuthentication()
            //            .UseAuthorization()
            .UseGiraffe routes


// 主程序
[<EntryPoint>]
let main args =
    Dapper.FSharp.OptionTypes.register ()

    /// Create temp path
    if not (Directory.Exists Config.TEMP_FILE_PATH) then
        Directory.CreateDirectory Config.TEMP_FILE_PATH
        |> ignore

    //    Zk.main ()

    /// Start transporter
    Transporter.Transporter |> ignore

    WebHostBuilder()
        .UseConfiguration(
            ConfigurationBuilder()
                .AddCommandLine(args)
                .Build()
        )
        .UseKestrel(fun opt ->
            opt.AddServerHeader <- false
            opt.Limits.MaxRequestBodySize <- 1024L * 1024L * 1024L)
        .UseStartup<Startup>()
        .ConfigureLogging(fun loggerBuilder ->
            loggerBuilder
                .AddFilter(fun lvl -> true)
                .AddConsole()
                .AddDebug()
            |> ignore)
        .Build()
        .Run()

    0
