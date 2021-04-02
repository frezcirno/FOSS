namespace CloudStorage.Server

open Microsoft.AspNetCore.Builder
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

    // This method gets called by the runtime. Use this method to add services to the container.
    member _.ConfigureServices(services: IServiceCollection) =
        services
            .AddResponseCompression()
            |> ignore
        
        services
            .AddIdentity()
            .AddRoles()
            |> ignore            
            
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, fun opt ->
                opt.LoginPath <- PathString "/Account/Unauthorized"
                opt.AccessDeniedPath <- PathString "/Account/Forbidden"
            )
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, fun opt ->
                opt.Audience <- "http://localhost:5001/"
                opt.Authority <- "http://localhost:5000/"
            )
            |> ignore
            
        services
            .AddGiraffe()
            |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member _.Configure (app: IApplicationBuilder) (env: IHostEnvironment) (loggerFactory: ILoggerFactory) =
        if env.IsDevelopment() then
            app
                .UseDeveloperExceptionPage()
                |> ignore

        app
            .UseGiraffe Router.routes
