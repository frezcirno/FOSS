namespace CloudStorage.Server

open CloudStorage.Server
open System
open System.Text
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Giraffe
open Microsoft.IdentityModel.Tokens

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
            .UseGiraffe Router.routes
