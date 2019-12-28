namespace STI.Middlewares

open STI.Env
open DomainAgnostic
open DotNetExtensions
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging


module Rewrite =

    type RewriteMiddleware(next: RequestDelegate, logger: ILogger<RewriteMiddleware>, deps: Container<Variables>) =

        let extract find =
            maybe {
                let! m = find "X-Forwarded-Method"
                let! p = find "X-Forwarded-Uri"
                let method = m |> ToString

                let path =
                    p
                    |> ToString
                    |> Result.New PathString
                    |> Option.OfResult
                    |> Option.defaultValue (PathString(""))

                return method, path
            }

        member __.InvokeAsync(ctx: HttpContext) =
            let task =
                async {
                    let headers, _ = Exts.Metadata ctx.Request

                    let method, path =
                        flip Map.tryFind headers
                        |> extract
                        |> option.ForceUnwrap "Missing Traefik Headers"
                    ctx.Request.Method <- method
                    ctx.Request.Path <- path

                    do! next.Invoke(ctx) |> Async.AwaitTask
                }
            task |> Async.StartAsTask
