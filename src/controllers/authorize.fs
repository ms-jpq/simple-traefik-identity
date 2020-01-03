namespace STI.Controllers

open STI
open STI.Env
open STI.Consts
open STI.Models.JWT
open STI.Models.Auth
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
type Authorize(logger: ILogger<Authorize>, deps: Container<Variables>) =
    inherit Controller()


    let cookie = deps.Boxed.cookie
    let jwt = deps.Boxed.jwt
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

    let authQuery (req: HttpRequest) =
        let find =
            Exts.Query req
            |> (flip Map.tryFind)
            |> (<<) (Option.map string)
        maybe {
            let! uri = find "redirect-uri" |> Option.map base64decode
            let! token = find "token"
            do! token
                |> readJWT jwt
                |> Option.map ignore
            return token, uri
        }


    [<HttpGet("")>]
    member self.Index() =
        async {
            let req, resp, conn = Exts.Ctx self.HttpContext
            let domain = req.Host |> string

            let authStatus =
                Exts.Cookies req
                |> Map.tryFind cookie.name
                |> Option.Recover ""
                |> checkAuth deps.Boxed.jwt domain

            match authStatus with
            | Authorized -> return StatusCodes.Status200OK |> StatusCodeResult :> IActionResult
            | Unauthorized ->
                Unauthorized
                |> string
                |> redirect req resp
                return StatusCodes.Status307TemporaryRedirect |> StatusCodeResult :> IActionResult
            | Unauthenticated ->
                Unauthenticated
                |> string
                |> redirect req resp
                return StatusCodes.Status307TemporaryRedirect |> StatusCodeResult :> IActionResult
        }
        |> Async.StartAsTask


    [<HttpGet(AUTHNAME)>]
    member self.Auth() =
        async {
            let req, resp, conn = Exts.Ctx self.HttpContext
            let auth = authQuery req

            match auth with
            | Some(token, uri) ->
                let policy =
                    req.Host
                    |> string
                    |> cookiePolicy

                resp.Cookies.Append(cookie.name, token, policy)
                [ "Location", uri ] |> flip Exts.AddHeaders resp

                return StatusCodes.Status307TemporaryRedirect |> StatusCodeResult :> IActionResult
            | None -> return StatusCodes.Status400BadRequest |> StatusCodeResult :> IActionResult
        }
        |> Async.StartAsTask


    [<HttpGet(DEAUTHNAME)>]
    member self.Deauth() =
        async {
            let req, resp, conn = Exts.Ctx self.HttpContext
            let uri = sprintf "%s://%A:%d" req.Scheme req.Host conn.RemotePort

            let policy =
                req.Host
                |> string
                |> cookiePolicy

            resp.Cookies.Delete(cookie.name, policy)
            [ "Location", uri ] |> flip Exts.AddHeaders resp

            return StatusCodes.Status307TemporaryRedirect |> StatusCodeResult :> IActionResult
        }
        |> Async.StartAsTask
