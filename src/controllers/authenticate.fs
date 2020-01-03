namespace STI.Controllers

open STI.Env
open STI.Models.Auth
open STI.Models.RateLimit
open STI.State
open STI.Views
open DomainAgnostic
open DomainAgnostic.Globals
open DotNetExtensions
open DotNetExtensions.Routing
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.Extensions.Logging
open System



[<Controller>]
type Authenticate(logger: ILogger<Authenticate>, deps: Container<Variables>, state: GlobalVar<State>) =
    inherit Controller()

    let cookie = deps.Boxed.cookie
    let jwt = deps.Boxed.jwt
    let model = deps.Boxed.model
    let display = deps.Boxed.display

    let policy (domain: string) =
        let policy = CookieOptions()
        policy.Path <- "/"
        policy.MaxAge <- cookie.maxAge |> Nullable
        policy.Domain <-
            model.baseDomains
            |> Seq.tryFind (fun d -> domain.EndsWith(d))
            |> Option.Recover domain

        policy


    [<HttpPost("")>]
    [<HttpHeader("Sti-Authorization")>]
    member self.Authenticate() =
        async {
            let req, resp, conn = Exts.Ctx self.HttpContext
            resp.StatusCode <- StatusCodes.Status418ImATeapot

            let domain = req.Host |> string

            let token =
                Exts.Headers req
                |> Map.MapValues string
                |> Map.tryFind "Sti-Authorization"
                |> Option.bind (newToken jwt model)

            let ip = conn.RemoteIpAddress |> string

            let! st = state.Get()
            let go, ns = ip |> next deps.Boxed.rateLimit st.history

            let s2 = { history = ns }
            do! state.Put(s2) |> Async.Ignore

            match (go, token) with
            | (true, Some tkn) ->
                req.GetDisplayUrl()
                |> sprintf "🦄 -- Authenticated -- 🦄\n%s"
                |> logger.LogWarning
                resp.Cookies.Append(cookie.name, tkn, policy domain)
                return {| ok = true |} |> JsonResult :> ActionResult
            | _ ->
                let uri = req.GetDisplayUrl()
                ns
                |> Map.tryFind ip
                |> Option.Recover Seq.empty
                |> fun x -> String.Join("\n", x)
                |> sprintf "⛔️ -- Authentication Failure -- ⛔️\n%s\n%s" uri
                |> logger.LogWarning
                return {| ok = false
                          timeout = not go |} |> JsonResult :> ActionResult
        }
        |> Async.StartAsTask


    [<Route("{*url}")>]
    member self.Login() =
        async {
            let req, resp, conn = Exts.Ctx self.HttpContext
            let domain = req.Host |> string

            let state =
                Exts.Cookies req
                |> Map.tryFind cookie.name
                |> Option.bind (checkAuth jwt domain)

            match state with
            | Some Authorized ->
                assert (false)
                return StatusCodes.Status204NoContent |> StatusCodeResult :> ActionResult
            | Some Unauthorized ->
                req.GetDisplayUrl()
                |> sprintf "🔐 -- Unauthorized -- 🔐\n%s"
                |> logger.LogInformation
                let html = Unauthorized.Render display
                resp.StatusCode <- StatusCodes.Status403Forbidden
                return self.Content(html, "text/html") :> ActionResult
            | Some Unauthenticated
            | _ ->
                req.GetDisplayUrl()
                |> sprintf "🔑 -- Authenticating -- 🔑\n%s"
                |> logger.LogInformation
                let html = Login.Render display
                resp.StatusCode <- StatusCodes.Status401Unauthorized
                return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask
