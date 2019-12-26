namespace STI

open DomainAgnostic
open DomainAgnostic.Globals
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open System
open STI.Env
open STI.Consts
open Microsoft.Extensions.Hosting

[<RequireQualifiedAccess>]
module Server =

    let private confLogging level (logging: ILoggingBuilder) =
        logging.AddFilter((<=) level) |> ignore
        logging.AddConsole() |> ignore


    let private confServices deps (services: IServiceCollection) =
        services.AddSingleton(Container deps) |> ignore
        services.AddControllers() |> ignore


    let private confApp baseUri (app: IApplicationBuilder) =
        app.UseStatusCodePages().UseDeveloperExceptionPage() |> ignore
        app.UsePathBase(baseUri) |> ignore
        app.UseStaticFiles() |> ignore
        app.UseRouting() |> ignore
        app.UseCors() |> ignore
        app.UseEndpoints(fun ep -> ep.MapControllers() |> ignore) |> ignore


    let private confWebhost deps (webhost: IWebHostBuilder) =
        webhost.UseWebRoot(RESOURCESDIR) |> ignore
        webhost.UseKestrel() |> ignore
        webhost.UseUrls(sprintf "http://0.0.0.0:%d" deps.port) |> ignore
        webhost.ConfigureServices(confServices deps) |> ignore
        webhost.Configure(Action<IApplicationBuilder>(confApp (PathString "/"))) |> ignore


    let Build<'D>(deps: Variables) =
        let host = Host.CreateDefaultBuilder()
        host.UseContentRoot(CONTENTROOT) |> ignore
        host.ConfigureLogging(confLogging deps.logLevel) |> ignore
        host.ConfigureWebHostDefaults(Action<IWebHostBuilder>(confWebhost deps)) |> ignore
        host.Build()
