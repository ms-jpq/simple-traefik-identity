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

    let authUri = deps.Boxed.baseuri
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
        let query =
            let builder = authUri |> UriBuilder
            builder.Query <-
                [ "redirect-uri", req.GetEncodedUrl() |> base64encode
                  "redirect-reason", reason ]
                |> Map.ofSeq
                |> Map.ToKVP
                |> QueryString.Create
                |> string
            builder |> string

        [ "Location", query ] |> flip Exts.AddHeaders resp


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

                req.GetDisplayUrl()
                |> sprintf "💁‍♀️ -- Allowed -- 💁‍♀️\n%s"
                |> logger.LogInformation


                return StatusCodes.Status401Unauthorized |> StatusCodeResult :> IActionResult
            | Unauthenticated ->

                req.GetDisplayUrl()
                |> sprintf "🙅‍♀️ -- Denied -- 🙅‍♀️\n%s"
                |> logger.LogWarning

                Unauthorized
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
                uri
                |> sprintf "🔑 -- Authorized -- 🔑\n%s"
                |> logger.LogWarning

                let policy =
                    req.Host
                    |> string
                    |> cookiePolicy

                resp.Cookies.Append(cookie.name, token, policy)
                [ "Location", uri ] |> flip Exts.AddHeaders resp


                return StatusCodes.Status307TemporaryRedirect |> StatusCodeResult :> IActionResult
            | None ->
                "⚠️ -- Invalid Auth Info -- ⚠️" |> logger.LogError
                return StatusCodes.Status400BadRequest |> StatusCodeResult :> IActionResult
        }
        |> Async.StartAsTask


    [<HttpGet(DEAUTHNAME)>]
    member self.Deauth() =
        async {
            let req, resp, conn = Exts.Ctx self.HttpContext

            req.GetDisplayUrl()
            |> sprintf "🔐 -- Deauthorized -- 🔐\n%s"
            |> logger.LogWarning

            let policy =
                req.Host
                |> string
                |> cookiePolicy

            resp.Cookies.Delete(cookie.name, policy)
            [ "Location", "/" ] |> flip Exts.AddHeaders resp

            return StatusCodes.Status307TemporaryRedirect |> StatusCodeResult :> IActionResult
        }
        |> Async.StartAsTask
