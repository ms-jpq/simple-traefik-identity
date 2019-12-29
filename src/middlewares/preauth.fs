namespace STI.Middlewares

open STI
open STI.Env
open STI.Auth
open DomainAgnostic
open DotNetExtensions
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.Extensions.Logging
open System


module Preauth =

    type PreauthMiddleware(next: RequestDelegate, logger: ILogger<PreauthMiddleware>, deps: Container<Variables>) =


        member __.InvokeAsync(ctx: HttpContext) =
            let task =
                async {
                    let req = ctx.Request
                    let _, cookies = Exts.Metadata req

                    let referer =
                        req.GetTypedHeaders().Referer
                        |> Option.OfNullable
                        |> Option.defaultValue (Uri("about:blank"))

                    let domain = req.Host |> ToString
                    let path = req.Path |> ToString
                    let authStatus = checkAuth deps.Boxed.jwt deps.Boxed.cookie domain cookies
                    let logout = deps.Boxed.logoutUri

                    let branch =
                        let c1 = logout.Host = domain && logout.LocalPath = path
                        let c2 = logout.Host = referer.Host && logout.LocalPath = referer.LocalPath
                        c1 || c2

                    match (branch, authStatus) with
                    | (false, AuthState.Authorized) ->
                        let uri = req.GetDisplayUrl() |> ToString
                        let info = sprintf "%s - %s :: %A" req.Method uri authStatus
                        logger.LogInformation info
                    | _ -> do! next.Invoke(ctx) |> Async.AwaitTask
                }
            task |> Async.StartAsTask
