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
[<Port(WEBSRVPORT)>]
type Authenticate(logger: ILogger<Authenticate>, deps: Container<Variables>, state: GlobalVar<State>) =
    inherit Controller()

    let cOpts = deps.Boxed.cookie
    let jOpts = deps.Boxed.jwt
    let model = deps.Boxed.model



    [<HttpGet("")>]
    member self.Index() =
        async {
            let req, resp, conn = Exts.Ctx self.HttpContext


            let html = ""
            return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask


    [<HttpPost("/authenticate")>]
    member self.Authenticate() =
        async {
            let req, resp, conn = Exts.Ctx self.HttpContext

            let token = None

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

            | _ ->
                req.GetDisplayUrl()
                |> sprintf "⛔️ -- Authentication Attempt -- ⛔️\n%s"
                |> logger.LogWarning

            return StatusCodes.Status307TemporaryRedirect |> StatusCodeResult :> IActionResult
        }
        |> Async.StartAsTask
