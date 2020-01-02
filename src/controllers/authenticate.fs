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


module Ingress =

    [<CLIMutable>]
    type LoginHeaders =
        { [<FromHeader(Name = "STI-Authorization")>]
          authorization: string }


open Ingress


[<Controller>]
[<Port(WEBSRVPORT)>]
type Authenticate(logger: ILogger<Authenticate>, deps: Container<Variables>, state: GlobalVar<State>) =
    inherit Controller()

    let cOpts = deps.Boxed.cookie
    let jOpts = deps.Boxed.jwt
    let model = deps.Boxed.model


    let render authState (req: HttpRequest) =
        let uri = req.GetDisplayUrl() |> string
        let info = sprintf "%A - %A" uri authState

        match (authState) with
        | AuthState.Authorized ->
            let html = Logout.Render deps.Boxed.display
            logger.LogInformation info
            html
        | AuthState.Unauthorized ->
            logger.LogWarning info
            let html = "" |> Unauthorized.Render deps.Boxed.display
            html
        | AuthState.Unauthenticated
        | _ ->
            logger.LogWarning info
            let html = req.GetEncodedUrl() |> Login.Render deps.Boxed.display
            html



    [<HttpGet("")>]
    member self.Index() =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            let host = req.Host |> string

            let authState =
                Exts.Cookies self.HttpContext.Request
                |> Map.tryFind cOpts.name
                |> Option.Recover ""
                |> checkAuth jOpts host

            let html = render authState req
            return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask


    [<HttpPost("/authenticate")>]
    member self.Login(credentials: LoginHeaders) =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            let conn = self.HttpContext.Connection


            let uri = req.GetDisplayUrl() |> string
            let token = credentials.authorization |> newToken jOpts model

            let! st = state.Get()
            let go, ns =
                conn.RemoteIpAddress
                |> string
                |> next deps.Boxed.rateLimit st.history

            let s2 = { history = ns }
            do! state.Put(s2) |> Async.Ignore

            match (go, token) with
            | (true, Some tkn) ->
                let info =
                    sprintf "🦄 -- Authenticated -- 🦄\n%A" uri

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


    [<HttpPost("/deauthenticate")>]
    member self.Logout() =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response

            let uri = req.GetDisplayUrl() |> string

            let info =
                sprintf "👋 -- Deauthenticated -- 👋\n%A" uri


            logger.LogWarning info
            return {| ok = true |} |> JsonResult :> ActionResult
        }
        |> Async.StartAsTask
