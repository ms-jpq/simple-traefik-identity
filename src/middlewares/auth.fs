namespace STI.Middlewares

open STI.Env
open STI.Models.Auth
open DomainAgnostic
open DotNetExtensions
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.Extensions.Logging


module Auth =

    type AuthMiddleware(next: RequestDelegate, logger: ILogger<AuthMiddleware>, deps: Variables Container) =

        let cookie = deps.Boxed.cookie
        let jwt = deps.Boxed.jwt
        let model = deps.Boxed.model

        member __.InvokeAsync(ctx: HttpContext) =
            let task =
                async {
                    let req, resp, conn = Exts.Ctx ctx
                    let domain = req.Host |> string

                    let state =
                        Exts.Cookies req
                        |> Map.tryFind cookie.name
                        |> Option.bind (checkAuth jwt model domain)

                    match state with
                    | Some Authorized ->
                        req.GetDisplayUrl()
                        |> sprintf "✅ -- Authorized -- ✅\n%s"
                        |> logger.LogInformation
                        resp.StatusCode <- StatusCodes.Status204NoContent
                    | _ -> do! next.Invoke(ctx) |> Async.AwaitTask
                }
            task |> Async.StartAsTask
