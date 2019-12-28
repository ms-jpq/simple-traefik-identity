namespace STI.Middlewares

open STI.Env
open DomainAgnostic
open DotNetExtensions
open Microsoft.AspNetCore.Http
open System.Net

module Rewrite =

    type RewriteMiddleware(next: RequestDelegate) =

        member __.InvokeAsync(ctx: HttpContext) =
            let task =
                async {
                    let req = ctx.Request
                    let conn = ctx.Connection
                    let headers, _ = Exts.Metadata req
                    let find = flip Map.tryFind headers

                    conn.RemoteIpAddress <-
                        find "X-Forwarded-For"
                        |> Option.map ToString
                        |> Option.bind ((Result.New IPAddress.Parse) >> Option.OfResult)
                        |> Option.defaultValue conn.RemoteIpAddress

                    conn.RemotePort <-
                        find "X-Forwarded-Port"
                        |> Option.map ToString
                        |> Option.bind Parse.Int
                        |> Option.defaultValue conn.RemotePort

                    req.Scheme <-
                        find "X-Forwarded-Proto"
                        |> Option.map ToString
                        |> Option.defaultValue req.Scheme

                    req.Method <-
                        find "X-Forwarded-Method"
                        |> Option.map ToString
                        |> Option.defaultValue req.Method

                    req.Host <-
                        find "X-Forwarded-Host"
                        |> Option.map ToString
                        |> Option.bind ((Result.New HostString) >> Option.OfResult)
                        |> Option.defaultValue req.Host

                    req.Path <-
                        find "X-Forwarded-Uri"
                        |> Option.map ToString
                        |> Option.bind ((Result.New PathString) >> Option.OfResult)
                        |> Option.defaultValue req.Path


                    do! next.Invoke(ctx) |> Async.AwaitTask
                }
            task |> Async.StartAsTask
