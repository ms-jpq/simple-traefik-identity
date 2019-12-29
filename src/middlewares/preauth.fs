namespace STI.Middlewares

open STI
open STI.Env
open STI.Auth
open DomainAgnostic
open DotNetExtensions
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.Extensions.Logging

module Preauth =

    type PreauthMiddleware(next: RequestDelegate, logger: ILogger<PreauthMiddleware>, deps: Container<Variables>) =


        member __.InvokeAsync(ctx: HttpContext) =
            let task =
                async {
                    let req = ctx.Request
                    let headers, cookies = Exts.Metadata req

                    let domain = req.Host |> ToString
                    let path = req.Path |> ToString
                    let authStatus = checkAuth deps.Boxed.jwt deps.Boxed.cookie domain cookies

                    let branch =
                        match deps.Boxed.logoutUri with
                        | Some l -> l.Host = domain && l.LocalPath = path
                        | None -> false

                    match (branch, authStatus) with
                    | (false, AuthState.Authorized) ->
                        let uri = req.GetDisplayUrl() |> ToString
                        let info = sprintf "%s - %s :: %A" req.Method uri authStatus
                        logger.LogInformation info
                    | _ -> do! next.Invoke(ctx) |> Async.AwaitTask
                }
            task |> Async.StartAsTask
