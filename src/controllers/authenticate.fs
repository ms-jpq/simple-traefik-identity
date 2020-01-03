namespace STI.Controllers

open STI.Env
open STI.Models.Auth
open STI.Models.RateLimit
open STI.State
open STI.Views
open STI.Consts
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


    [<Route("{*url}")>]
    member self.Index() =
        async {
            let req, resp, conn = Exts.Ctx self.HttpContext
            let domain = req.Host |> string

            let state =
                Exts.Cookies req
                |> Map.tryFind cookie.name
                |> Option.map (checkAuth jwt domain)

            match state with
            | Some Authorized ->
                assert (false)
                return StatusCodes.Status204NoContent |> StatusCodeResult :> ActionResult
            | Some Unauthorized ->
                req.GetDisplayUrl()
                |> sprintf "🔐 -- Unauthorized -- 🔐\n%s"
                |> logger.LogInformation
                let html = Unauthorized.Render display
                resp.StatusCode <- StatusCodes.Status418ImATeapot
                return self.Content(html, "text/html") :> ActionResult
            | Some Unauthenticated
            | _ ->
                req.GetDisplayUrl()
                |> sprintf "🔑 -- Authenticating -- 🔑\n%s"
                |> logger.LogInformation
                let html = Login.Render display
                resp.StatusCode <- StatusCodes.Status418ImATeapot
                return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask


    [<HttpHeader("STI-Authenticate")>]
    member self.Authenticate() =
        async {
            let req, resp, conn = Exts.Ctx self.HttpContext
            resp.StatusCode <- StatusCodes.Status418ImATeapot

            let domain = req.Host |> string

            let token =
                Exts.Headers req
                |> Map.MapValues string
                |> Map.tryFind "STI-Authenticate"
                |> Option.bind (newToken jwt model)

            let! st = state.Get()
            let go, ns =
                conn.RemoteIpAddress
                |> string
                |> next deps.Boxed.rateLimit st.history

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
                req.GetDisplayUrl()
                |> sprintf "⛔️ -- Authentication Failure -- ⛔️\n%s"
                |> logger.LogWarning
                return {| ok = false
                          timeout = not go |} |> JsonResult :> ActionResult
        }
        |> Async.StartAsTask
