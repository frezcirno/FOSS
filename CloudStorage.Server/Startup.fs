module CloudStorage.Server.Startup

open Microsoft.AspNetCore.Builder
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
            .AddGiraffe()
            |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member _.Configure (app: IApplicationBuilder) (env: IHostEnvironment) (loggerFactory: ILoggerFactory) =
        if env.IsDevelopment() then
            app
                .UseDeveloperExceptionPage()
                .UseSwagger()
                .UseSwaggerUI(fun c -> c.SwaggerEndpoint("/swagger/v1/swagger.json", "test v1"))
            |> ignore

        app
            .UseGiraffe Router.routes
