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
                    let authStatus = checkAuth deps.Boxed.jwt deps.Boxed.cookie domain cookies

                    match authStatus with
                    | AuthState.Authorized ->
                        let uri = req.GetDisplayUrl() |> ToString
                        let info = sprintf "%s - %s :: %A" req.Method uri authStatus
                        logger.LogInformation info
                    | _ -> do! next.Invoke(ctx) |> Async.AwaitTask
                }
            task |> Async.StartAsTask
