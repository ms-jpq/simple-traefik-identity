namespace STI.Controllers

open STI
open STI.Env
open STI.Auth
open STI.State
open STI.Views
open STI.RateLimit
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

        static member Decode headers =
            try
                let credentials = headers.authorization.Split(" ")
                if credentials.[0] <> "Basic" then failwith "..."

                let decoded =
                    credentials.[1]
                    |> Convert.FromBase64String
                    |> Encoding.UTF8.GetString
                    |> fun s -> s.Split(":")

                let username = decoded.[0]
                let password = decoded.[1]
                (username, password) |> Some

            with _ -> None


open Ingress


[<Route("")>]
type Entry(logger: ILogger<Entry>, deps: Container<Variables>, state: GlobalVar<State>) =
    inherit Controller()

    let cOpts = deps.Boxed.cookie
    let jOpts = deps.Boxed.jwt

    let cookiePolicy (domain: string) =
        let policy = CookieOptions()
        policy.MaxAge <- cOpts.maxAge |> Nullable
        policy.Path <- "/"
        policy.Domain <-
            deps.Boxed.model.domains
            |> Seq.tryFind (fun d -> domain.EndsWith(d))
            |> Option.defaultValue domain

        policy


    let login username password =
        let seek (u: User) = u.name = username && u.password = password
        deps.Boxed.model.users |> Seq.tryFind seek

    let findToken credentials =
        credentials
        |> LoginHeaders.Decode
        |> Option.bind ((<||) login)
        |> Option.map (fun u -> { access = u.subDomains })
        |> Option.map JwtClaim.Serialize
        |> Option.map (newJWT jOpts)


    let render authState (req: HttpRequest) =
        let logout = deps.Boxed.logoutUri
        let uri = req.GetDisplayUrl() |> ToString
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
            let host = req.Host |> ToString
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

            let headers, _ = Exts.Metadata req
            let uri = req.GetDisplayUrl() |> ToString
            let token = credentials |> findToken

            let policy =
                req.Host
                |> ToString
                |> cookiePolicy

            let! st = state.Get()
            let go, ns =
                headers
                |> Map.tryFind deps.Boxed.rateLimit.header
                |> Option.map ToString
                |> Option.defaultValue (conn.RemoteIpAddress.ToString())
                |> next deps.Boxed.rateLimit st
            do! state.Put(ns) |> Async.Ignore

            match (go, token) with
            | (true, Some tkn) ->
                let info =
                    sprintf "🦄 -- Authenticated -- 🦄\n%A" uri

                resp.Cookies.Append(cOpts.name, tkn, policy)
                logger.LogWarning info
                return {| ok = true |} |> JsonResult :> ActionResult
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

            let uri = req.GetDisplayUrl() |> ToString

            let info =
                sprintf "👋 -- Deauthenticated -- 👋\n%A" uri

            let policy =
                req.Host
                |> ToString
                |> cookiePolicy

            resp.Cookies.Delete(cOpts.name, policy)
            logger.LogWarning info
            return {| ok = true |} |> JsonResult :> ActionResult
        }
        |> Async.StartAsTask
