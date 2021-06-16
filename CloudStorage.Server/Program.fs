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
            .UseGiraffe Handler.routes


// 主程序
[<EntryPoint>]
let main argv =
    Dapper.FSharp.OptionTypes.register ()

    //    Zk.main ()
    Program.main null |> ignore /// 1
    Program.main null |> ignore /// 2
    Program.main null |> ignore /// 3

    Host
        .CreateDefaultBuilder(argv)
        .ConfigureAppConfiguration(fun config -> ())
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .UseStartup<Startup>()
                .UseKestrel(fun k -> k.AddServerHeader <- false)
            |> ignore)
        .ConfigureLogging(fun loggerBuilder ->
            loggerBuilder
                .AddFilter(fun lvl -> true)
                .AddConsole()
                .AddDebug()
            |> ignore)
        .Build()
        .Run()

    0
