namespace STI.Middlewares

open STI
open STI.Env
open DomainAgnostic
open DomainAgnostic.Globals
open DotNetExtensions
open DotNetExtensions.Routing
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.IdentityModel.Tokens
open System
open System.Text
open System.IdentityModel.Tokens.Jwt


module Preauth =

    type PreauthMiddleware(next: RequestDelegate, logger: ILogger<PreauthMiddleware>, deps: Container<Variables>) =


        member __.InvokeAsync(ctx: HttpContext) =
            let task =
                async {
                    let headers, cookies = Exts.Metadata ctx.Request
                    do! next.Invoke(ctx) |> Async.AwaitTask }
            task |> Async.StartAsTask
