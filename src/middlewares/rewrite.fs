namespace STI.Middlewares

open STI.Env
open DomainAgnostic
open DotNetExtensions
open Microsoft.AspNetCore.Http
open System.Net

module Rewrite =

    type RewriteMiddleware(next: RequestDelegate, deps: Variables Container) =

        let ipHeaders = deps.Boxed.rateLimit.headers

        member __.InvokeAsync(ctx: HttpContext) =
            let task =
                async {
                    let req, resp, conn = Exts.Ctx ctx
                    let find = Exts.Headers req |> flip Map.tryFind

                    conn.RemoteIpAddress <-
                        ipHeaders
                        |> Seq.tryPick find
                        |> Option.orElse (find "X-Forwarded-For")
                        |> Option.bind
                            (string
                             >> (Result.New IPAddress.Parse)
                             >> Option.OfResult)
                        |> Option.Recover conn.RemoteIpAddress

                    conn.RemotePort <-
                        find "X-Forwarded-Port"
                        |> Option.bind (string >> Parse.Int)
                        |> Option.Recover conn.RemotePort

                    req.Scheme <-
                        find "X-Forwarded-Proto"
                        |> Option.map string
                        |> Option.Recover req.Scheme

                    req.Method <-
                        find "X-Forwarded-Method"
                        |> Option.map string
                        |> Option.Recover req.Method

                    req.Host <-
                        find "X-Forwarded-Host"
                        |> Option.bind
                            (string
                             >> (Result.New HostString)
                             >> Option.OfResult)
                        |> Option.Recover req.Host

                    req.Path <-
                        find "X-Forwarded-Uri"
                        |> Option.bind
                            (string
                             >> (Result.New PathString)
                             >> Option.OfResult)
                        |> Option.Recover req.Path


                    do! next.Invoke(ctx) |> Async.AwaitTask
                }
            task |> Async.StartAsTask
