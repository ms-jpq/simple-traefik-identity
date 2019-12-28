namespace STI

open DomainAgnostic
open DomainAgnostic.Globals
open Microsoft.AspNetCore.CookiePolicy
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Hosting
open System
open STI.Env
open STI.Consts
open STI.Middlewares.Preauth
open STI.Middlewares.Rewrite



[<RequireQualifiedAccess>]
module Server =

    let private confLogging level (logging: ILoggingBuilder) =
        logging.AddFilter((<=) level) |> ignore
        logging.AddConsole() |> ignore


    let private confServices deps (globals: GlobalVar<'D>) (services: IServiceCollection) =
        services.AddSingleton(Container deps).AddSingleton(globals) |> ignore
        services.AddControllers() |> ignore

    let private confCookies =
        let options = CookiePolicyOptions()
        options.HttpOnly <- HttpOnlyPolicy.Always
        options.MinimumSameSitePolicy <- SameSiteMode.Lax
        options.Secure <- CookieSecurePolicy.SameAsRequest
        options

    let private confApp baseUri (app: IApplicationBuilder) =
        app.UseStatusCodePages().UseDeveloperExceptionPage() |> ignore
        app.UseMiddleware<RewriteMiddleware>() |> ignore
        app.UseMiddleware<PreauthMiddleware>() |> ignore
        app.UsePathBase(baseUri) |> ignore
        app.UseStaticFiles() |> ignore
        app.UseCookiePolicy(confCookies) |> ignore
        app.UseRouting() |> ignore
        app.UseCors() |> ignore
        app.UseEndpoints(fun ep -> ep.MapControllers() |> ignore) |> ignore


    let private confWebhost (deps: Variables) gloabls (webhost: IWebHostBuilder) =
        webhost.UseWebRoot(RESOURCESDIR) |> ignore
        webhost.UseKestrel() |> ignore
        webhost.UseUrls(sprintf "http://0.0.0.0:%d" deps.port) |> ignore
        webhost.ConfigureServices(confServices deps gloabls) |> ignore
        webhost.Configure(Action<IApplicationBuilder>(confApp (PathString "/"))) |> ignore


    let Build<'D> (deps: Variables) (globals: GlobalVar<'D>) =
        let host = Host.CreateDefaultBuilder()
        host.UseContentRoot(CONTENTROOT) |> ignore
        host.ConfigureLogging(confLogging deps.logLevel) |> ignore
        host.ConfigureWebHostDefaults(Action<IWebHostBuilder>(confWebhost deps globals)) |> ignore
        host.Build()
