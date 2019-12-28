namespace STI.Controllers

open STI
open STI.Env
open STI.Auth
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
    let authModel = deps.Boxed.model
    let renderReq = deps.Boxed.resources, deps.Boxed.background, deps.Boxed.title

    let cookiePolicy (domain: string) =
        let d =
            deps.Boxed.model.domains
            |> Seq.tryFind (fun d -> d.Contains(domain))
            |> Option.defaultValue domain

        let policy = CookieOptions()
        policy.MaxAge <- cOpts.maxAge |> Nullable
        policy.Domain <- d
        policy.Path <- null
        policy

    let login username password =
        let seek (u: User) = u.name = username && u.password = password
        authModel.users |> Seq.tryFind seek

    let render authState (req: HttpRequest) =
        let uri = req.GetDisplayUrl() |> ToString
        let info = sprintf "%A - %A" uri authState
        match authState with
        | AuthState.Authorized ->
            async {
                logger.LogInformation info
                return "", Seq.empty
            }
        | AuthState.Unauthorized ->
            async {
                logger.LogWarning info
                let! html = "" |> (|||>) renderReq Unauthorized.Render
                return html, Seq.empty
            }
        | AuthState.Unauthenticated
        | _ ->
            async {
                logger.LogWarning info
                let! html = req.GetEncodedUrl() |> (|||>) renderReq Login.Render
                return html, Seq.empty
            }

    [<Route("")>]
    member self.Index() =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            let _, cookies = Exts.Metadata self.HttpContext.Request
            let host = req.Host |> ToString
            let authState = checkAuth jOpts cOpts host cookies

            let! html, respHeaders = render authState req

            Exts.AddHeaders respHeaders resp
            resp.StatusCode <- authState |> LanguagePrimitives.EnumToValue
            return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask


    [<HttpPost("")>]
    [<HttpHeader("STI-Authorization")>]
    member self.Login(credentials: LoginHeaders) =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            resp.StatusCode <- StatusCodes.Status418ImATeapot

            let uri = req.GetDisplayUrl() |> ToString

            let token =
                credentials
                |> LoginHeaders.Decode
                |> Option.bind ((<||) login)
                |> Option.map (fun u -> { access = u.domains })
                |> Option.map JwtClaim.Serialize
                |> Option.map (newJWT jOpts)


            match token with
            | Some tkn ->
                let info =
                    sprintf "🦄 -- Authenticated -- 🦄\n%A" uri

                let policy =
                    req.Host
                    |> ToString
                    |> cookiePolicy

                resp.Cookies.Append(cOpts.name, tkn, policy)
                logger.LogWarning info
                return {| ok = true |} |> JsonResult :> ActionResult
            | None ->
                let info =
                    sprintf "⛔️ -- Authentication Attempt -- ⛔️\n%A" uri
                logger.LogWarning info
                return {| ok = false |} |> JsonResult :> ActionResult
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
