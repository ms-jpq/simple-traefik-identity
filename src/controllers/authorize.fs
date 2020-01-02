namespace STI.Controllers

open STI
open STI.Env
open STI.Consts
open STI.Models.Authorize
open DomainAgnostic
open DomainAgnostic.Encode
open DotNetExtensions
open DotNetExtensions.Routing
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Mvc
open System


[<Controller>]
[<Port(AUTHSRVPORT)>]
type Entry(logger: ILogger<Entry>, deps: Container<Variables>) =
    inherit Controller()


    let cookie = deps.Boxed.cookie
    let jtw = deps.Boxed.jwt
    let model = deps.Boxed.model

    let cookiePolicy (domain: string) =
        let policy = CookieOptions()
        policy.MaxAge <- cookie.maxAge |> Nullable
        policy.Path <- "/"
        policy.Domain <-
            model.baseDomains
            |> Seq.tryFind (fun d -> domain.EndsWith(d))
            |> Option.Recover domain

        policy

    let redirect (req: HttpRequest) (resp: HttpResponse) reason =
        let uri = req.GetEncodedUrl() |> base64encode

        let args =
            [ "redirect-uri", uri
              "redirect-reason", reason ]
            |> Map.ofSeq
            |> Map.ToKVP

        let query = QueryString.Create(args).ToString()
        let headers = [ "Location", query ]
        Exts.AddHeaders headers resp

    let queryRedirect (req: HttpRequest) =
        let find = Exts.Query req |> flip Map.tryFind
        maybe {
            let! uri = find "redirect-uri" |> Option.map (string >> base64decode)
            let! token = find "token" |> Option.map string
            return token, uri }



    [<HttpGet("")>]
    member self.Index() =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            let domain = req.Host |> string

            let authStatus =
                Exts.Cookies req
                |> Map.tryFind cookie.name
                |> Option.Recover ""
                |> checkAuth deps.Boxed.jwt domain

            match authStatus with
            | AuthState.Authorized -> return StatusCodes.Status200OK |> StatusCodeResult :> IActionResult
            | AuthState.Unauthorized ->
                AuthState.Unauthorized
                |> string
                |> redirect req resp
                return StatusCodes.Status307TemporaryRedirect |> StatusCodeResult :> IActionResult
            | AuthState.Unauthenticated
            | _ ->
                AuthState.Unauthenticated
                |> string
                |> redirect req resp
                return StatusCodes.Status307TemporaryRedirect |> StatusCodeResult :> IActionResult
        }
        |> Async.StartAsTask


    [<HttpGet("_sti_auth")>]
    member self.Auth() =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            let p = queryRedirect req

            let policy =
                req.Host
                |> string
                |> cookiePolicy

            resp.Cookies.Delete(cookie.name, policy)

            return StatusCodes.Status307TemporaryRedirect |> StatusCodeResult :> IActionResult
        }
        |> Async.StartAsTask
