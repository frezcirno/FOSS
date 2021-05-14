namespace CloudStorage.Server

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Giraffe

type Startup(configuration: IConfiguration) =
    member _.Configuration = configuration

    member this.ConfigureServices(services: IServiceCollection) =
        services.AddResponseCompression() |> ignore

        services.AddIdentity().AddRoles() |> ignore

        services
            .AddAuthentication(fun opt ->
                opt.DefaultScheme <- CookieAuthenticationDefaults.AuthenticationScheme
                opt.DefaultAuthenticateScheme <- CookieAuthenticationDefaults.AuthenticationScheme
                opt.DefaultSignInScheme <- CookieAuthenticationDefaults.AuthenticationScheme
                opt.DefaultChallengeScheme <- CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(fun opt ->
                opt.Cookie.Name <- "Cookies"
                opt.LoginPath <- PathString "/user/signin"
                opt.LogoutPath <- PathString "/user/signout"
                opt.ExpireTimeSpan <- TimeSpan.FromHours(1.0))
            .AddJwtBearer(fun opt ->
                opt.Audience <- "http://localhost:5001/"
                opt.Authority <- "http://localhost:5000/"
                opt.SaveToken <- true)
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
