namespace STI.Controllers

open STI
open STI.Env
open STI.Consts
open STI.Views
open DomainAgnostic
open DomainAgnostic.Globals
open DomainAgnostic.Reflection
open DotNetExtensions
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Primitives
open Microsoft.IdentityModel.Tokens
open System
open System.IO
open System.Text
open System.IdentityModel.Tokens.Jwt


module Ingress =

    type AuthState =
        | Unauthenticated = 401
        | Unauthorized = 403
        | Authorized = 203

    type JwtClaim =
        { access: Domains }

        static member Serialize claim =
            match claim.access with
            | All -> [ "access", "*" :> obj ] |> Map.ofSeq
            | Named lst ->
                let access = String.Join(",", lst |> Seq.toArray) :> obj
                [ "access", access ] |> Map.ofSeq

        static member DeSerialize claim =
            maybe {
                let! access = claim |> Map.tryFind "access"
                let res =
                    match access with
                    | "*" -> { access = All }
                    | c ->
                        let a =
                            c.Split(",")
                            |> Seq.ofArray
                            |> Named
                        { access = a }
                return res
            }


open Ingress

[<Route("")>]
type Entry(logger: ILogger<Entry>, deps: Container<Variables>, state: GlobalVar<State>) =
    inherit Controller()

    let cOpts = deps.Boxed.cookie
    let jOpts = deps.Boxed.jwt
    let authModel = deps.Boxed.model
    let renderReq = deps.Boxed.background, deps.Boxed.title


    let cookiePolicy domain =
        let policy = CookieOptions()
        policy.HttpOnly <- true
        policy.SameSite <- SameSiteMode.Strict
        policy.Secure <- cOpts.secure
        policy.MaxAge <- cOpts.maxAge |> Nullable
        policy.Domain <- cOpts.domain |> Option.defaultValue domain
        policy

    let credentials =
        let bytes = authModel.secret |> Encoding.UTF8.GetBytes
        let key = SymmetricSecurityKey(bytes)
        let algo = SecurityAlgorithms.HmacSha256Signature
        SigningCredentials(key, algo)


    let validateJWT (token: string) =
        let validation = TokenValidationParameters()
        try
            let principal = JwtSecurityTokenHandler().ValidateToken(token, validation, ref null)
            principal.Claims
            |> Seq.map (fun c -> c.Type, c.Value)
            |> Map.ofSeq
            |> Some
        with _ -> None

    let newJWT claims =
        let now = DateTime.UtcNow

        let desc =
            let desc = SecurityTokenDescriptor()
            desc.SigningCredentials <- credentials
            desc.Issuer <- jOpts.issuer
            desc.IssuedAt <- now |> Nullable
            desc.Expires <- now + jOpts.lifespan |> Nullable
            desc.Claims <-
                claims
                |> Map.toSeq
                |> dict
            desc

        JwtSecurityTokenHandler().CreateEncodedJwt(desc)


    let checkAuth domain cookies =
        maybe {
            let! claims = cookies
                          |> Map.tryFind cOpts.name
                          |> Option.bind validateJWT
            let! model = JwtClaim.DeSerialize claims
            let auth =
                match model.access with
                | All -> AuthState.Authorized
                | Named lst ->
                    let chk =
                        lst
                        |> Set
                        |> flip Set.contains
                    match chk domain with
                    | true -> AuthState.Authorized
                    | false -> AuthState.Unauthorized
            return auth
        }
        |> Option.defaultValue AuthState.Unauthenticated


    let fdAuth headers =
        let err = "Missing Required Headers :: X-Forwarded-Host, X-Forwarded-Uri"
        maybe {
            let! domain = headers
                          |> Map.tryFind "X-Forwarded-Host"
                          |> Option.map ToString
            let! path = headers
                        |> Map.tryFind "X-Forwarded-Uri"
                        |> Option.map ToString
            return domain, path
        }
        |> Option.ForceUnwrap err


    [<HttpGet("")>]
    member self.Index() =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            let headers, cookies = Exts.Metadata req
            let domain, path = fdAuth headers
            let auth = checkAuth domain cookies

            let html =
                match auth with
                | AuthState.Authorized -> ""
                | AuthState.Unauthorized -> renderReq ||> Unauthorized.Render
                | _ -> renderReq ||> Login.Render

            resp.StatusCode <- LanguagePrimitives.EnumToValue auth
            return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask


    [<HttpPost("/login")>]
    member self.Login(username: string, password: string, goto: string) =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            let headers, cookies = Exts.Metadata req
            let domain, path = fdAuth headers
            let policy = cookiePolicy domain

            let token =
                authModel.users
                |> Seq.tryFind (fun u -> u.name = username && u.password = password)
                |> Option.map (fun u -> { access = u.domains })
                |> Option.map JwtClaim.Serialize
                |> Option.map newJWT

            match token with
            | Some t ->
                let headers = [ "Location", goto ]
                resp.Cookies.Append(cOpts.name, t, policy)
                resp.StatusCode <- 302
                Exts.AddHeaders headers resp
                return self.Content("", "text/html") :> ActionResult
            | None ->
                let html = renderReq ||> Login.Render
                resp.StatusCode <- LanguagePrimitives.EnumToValue AuthState.Unauthenticated
                return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask


    [<HttpPost("/logout")>]
    member self.Logout() =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            let headers, cookies = Exts.Metadata req
            let domain, path = fdAuth headers
            let policy = cookiePolicy domain
            let headers = [ "Refresh", "0" ]
            let html = renderReq ||> Login.Render

            Exts.AddHeaders headers resp
            resp.Cookies.Delete(cOpts.name, policy)
            return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask
