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
open STI.Middlewares.Rewrite



[<RequireQualifiedAccess>]
module Server =

    let private confLogging level (logging: ILoggingBuilder) =
        logging.AddFilter((<=) level) |> ignore
        logging.AddConsole() |> ignore


    let private confServices deps (globals: GlobalVar<'D>) (services: IServiceCollection) =
        services.AddSingleton(Container deps) |> ignore
        services.AddSingleton(globals) |> ignore
        services.AddControllers() |> ignore


    let private confAuthorize (deps: Variables) (app: IApplicationBuilder) =
        app.UseMiddleware<RewriteMiddleware>() |> ignore
        app.UseRouting() |> ignore
        app.UseEndpoints(fun ep -> ep.MapControllers() |> ignore) |> ignore


    let private confCookies =
        let options = CookiePolicyOptions()
        options.HttpOnly <- HttpOnlyPolicy.Always
        options.Secure <- CookieSecurePolicy.SameAsRequest
        options.MinimumSameSitePolicy <- SameSiteMode.Strict
        options


    let private confAuthenticate (deps: Variables) (app: IApplicationBuilder) =
        app.UseStatusCodePages() |> ignore
        app.UseDeveloperExceptionPage() |> ignore
        app.UsePathBase(deps.baseuri.LocalPath |> PathString) |> ignore
        app.UseStaticFiles() |> ignore
        app.UseCookiePolicy(confCookies) |> ignore
        app.UseRouting() |> ignore
        app.UseCors() |> ignore
        app.UseEndpoints(fun ep -> ep.MapControllers() |> ignore) |> ignore


    let private confApp deps (app: IApplicationBuilder) =
        let discriminate port (ctx: HttpContext) = ctx.Connection.LocalPort = port
        let f1 = Func<HttpContext, bool>(discriminate AUTHSRVPORT)
        let a1 = Action<IApplicationBuilder>(confAuthorize deps)
        let f2 = Func<HttpContext, bool>(discriminate WEBSRVPORT)
        let a2 = Action<IApplicationBuilder>(confAuthenticate deps)
        app.UseWhen(f1, a1) |> ignore
        app.UseWhen(f2, a2) |> ignore


    let private confWebhost (deps: Variables) gloabls (webhost: IWebHostBuilder) =
        let auth = sprintf "http://0.0.0.0:%d" AUTHSRVPORT
        let web = sprintf "http://0.0.0.0:%d" WEBSRVPORT
        webhost.UseWebRoot(RESOURCESDIR) |> ignore
        webhost.UseKestrel() |> ignore
        webhost.UseUrls(auth, web) |> ignore
        webhost.ConfigureServices(confServices deps gloabls) |> ignore
        webhost.Configure(Action<IApplicationBuilder>(confApp deps)) |> ignore


    let Build<'D> (deps: Variables) (globals: GlobalVar<'D>) =
        let host = Host.CreateDefaultBuilder()
        host.UseContentRoot(CONTENTROOT) |> ignore
        host.ConfigureLogging(confLogging deps.sys.logLevel) |> ignore
        host.ConfigureWebHostDefaults(Action<IWebHostBuilder>(confWebhost deps globals)) |> ignore
        host.Build()
