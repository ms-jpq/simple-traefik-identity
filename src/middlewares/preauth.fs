namespace STI.Middlewares

open STI
open STI.Env
open STI.Auth
open DomainAgnostic
open DotNetExtensions
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging


module Preauth =

    type PreauthMiddleware(next: RequestDelegate, logger: ILogger<PreauthMiddleware>, deps: Container<Variables>) =


        member __.InvokeAsync(ctx: HttpContext) =
            let task =
                async {
                    let headers, cookies = Exts.Metadata ctx.Request

                    let domain = ctx.Request.Host |> ToString
                    let authStatus = checkAuth deps.Boxed.jwt deps.Boxed.cookie domain cookies

                    match authStatus with
                    | AuthState.Authorized ->
                        let info = sprintf "%A" authStatus
                        logger.LogInformation info
                    | _ -> do! next.Invoke(ctx) |> Async.AwaitTask
                }
            task |> Async.StartAsTask
