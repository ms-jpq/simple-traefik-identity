namespace STI.Controllers

open STI.Env
open STI.Models.Authenticate
open STI.Models.Authorize
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
open System.Text


module Ingress =

    [<CLIMutable>]
    type LoginHeaders =
        { [<FromHeader(Name = "STI-Authorization")>]
          authorization: string }


open Ingress


[<Route("")>]
type Entry(logger: ILogger<Entry>, deps: Container<Variables>, state: GlobalVar<State>) =
    inherit Controller()

    let cOpts = deps.Boxed.cookie
    let jOpts = deps.Boxed.jwt
    let model = deps.Boxed.model

    let cookiePolicy (domain: string) =
        let policy = CookieOptions()
        policy.MaxAge <- cOpts.maxAge |> Nullable
        policy.Path <- "/"
        policy.Domain <-
            deps.Boxed.model.baseDomains
            |> Seq.tryFind (fun d -> domain.EndsWith(d))
            |> Option.Recover domain

        policy


    let render authState (req: HttpRequest) =
        let logout = deps.Boxed.logoutUri
        let uri = req.GetDisplayUrl() |> string
        let info = sprintf "%A - %A" uri authState
        let code = authState |> LanguagePrimitives.EnumToValue
        let branch = logout.Host = req.Host.ToString() && logout.LocalPath = req.Path.ToString()

        match (branch, authState) with
        | true, AuthState.Authorized ->
            async {
                let! html = Logout.Render deps.Boxed.display
                logger.LogInformation info
                return html, StatusCodes.Status418ImATeapot
            }
        | _, AuthState.Authorized ->
            async {
                logger.LogInformation info
                return "", code
            }
        | _, AuthState.Unauthorized ->
            async {
                logger.LogWarning info
                let! html = "" |> Unauthorized.Render deps.Boxed.display
                return html, code
            }
        | _, AuthState.Unauthenticated
        | _ ->
            async {
                logger.LogWarning info
                let! html = req.GetEncodedUrl() |> Login.Render deps.Boxed.display
                return html, code
            }


    [<HttpGet("{*.}")>]
    member self.Index() =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            let _, cookies = Exts.Metadata self.HttpContext.Request
            let host = req.Host |> string
            let authState = checkAuth jOpts cOpts host cookies

            let! html, code = render authState req
            resp.StatusCode <- code
            return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask


    [<HttpPost("")>]
    [<HttpHeader("STI-Authorization")>]
    member self.Login(credentials: LoginHeaders) =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            let conn = self.HttpContext.Connection
            resp.StatusCode <- StatusCodes.Status418ImATeapot


            let uri = req.GetDisplayUrl() |> string
            let token = credentials.authorization |> newToken jOpts model

            let policy =
                req.Host
                |> string
                |> cookiePolicy

            let! st = state.Get()
            let go, ns =
                conn.RemoteIpAddress
                |> string
                |> next deps.Boxed.rateLimit st
            do! state.Put(ns) |> Async.Ignore

            match (go, token) with
            | (true, Some tkn) ->
                let info =
                    sprintf "🦄 -- Authenticated -- 🦄\n%A" uri

                resp.Cookies.Append(cOpts.name, tkn, policy)
                logger.LogWarning info
                return {| ok = true
                          go = true |} |> JsonResult :> ActionResult
            | _ ->
                let info =
                    sprintf "⛔️ -- Authentication Attempt -- ⛔️\n%A" uri

                logger.LogWarning info
                return {| ok = false
                          go = go |} |> JsonResult :> ActionResult
        }
        |> Async.StartAsTask


    [<HttpPost("")>]
    [<HttpHeader("STI-Deauthorization")>]
    member self.Logout() =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            resp.StatusCode <- StatusCodes.Status418ImATeapot

            let uri = req.GetDisplayUrl() |> string

            let info =
                sprintf "👋 -- Deauthenticated -- 👋\n%A" uri

            let policy =
                req.Host
                |> string
                |> cookiePolicy

            resp.Cookies.Delete(cOpts.name, policy)
            logger.LogWarning info
            return {| ok = true |} |> JsonResult :> ActionResult
        }
        |> Async.StartAsTask
