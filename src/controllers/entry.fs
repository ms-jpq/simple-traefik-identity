namespace STI.Controllers

open STI
open STI.Env
open STI.Consts
open STI.Views
open DomainAgnostic
open DomainAgnostic.Globals
open DotNetExtensions
open DotNetExtensions.Routing
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
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

    [<CLIMutable>]
    type ForwardedHeaders =
        { [<FromHeader(Name = "X-Forwarded-For")>]
          origin: string
          [<FromHeader(Name = "X-Forwarded-Host")>]
          host: string
          [<FromHeader(Name = "X-Forwarded-Method")>]
          method: string
          [<FromHeader(Name = "X-Forwarded-Port")>]
          port: int
          [<FromHeader(Name = "X-Forwarded-Proto")>]
          scheme: string
          [<FromHeader(Name = "X-Forwarded-Server")>]
          proxy: string
          [<FromHeader(Name = "X-Forwarded-Uri")>]
          path: string
          [<FromHeader(Name = "X-Real-Ip")>]
          originIP: string }

        static member OriginalUri headers =
            sprintf "%s://%s:%d/%s" headers.scheme headers.host headers.port headers.path |> Uri


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


    let login username password =
        let seek (u: User) = u.name = username && u.password = password
        authModel.users |> Seq.tryFind seek


    [<Route("")>]
    member self.Index(headers: ForwardedHeaders) =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            let _, cookies = Exts.Metadata req
            let auth = checkAuth headers.host cookies

            let html =
                match auth with
                | AuthState.Authorized -> ""
                | AuthState.Unauthorized -> renderReq ||> Unauthorized.Render
                | _ -> renderReq ||> Login.Render

            resp.StatusCode <- LanguagePrimitives.EnumToValue auth
            return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask


    [<Route("")>]
    [<HttpHeader("STI-Login")>]
    member self.Login(headers: ForwardedHeaders, credentials: LoginHeaders) =
        async {
            let token =
                credentials
                |> LoginHeaders.Decode
                |> Option.bind (flip (||>) login)
                |> Option.map (fun u -> { access = u.domains })
                |> Option.map JwtClaim.Serialize
                |> Option.map newJWT

            match token with
            | Some t ->
                let policy = cookiePolicy headers.host
                self.HttpContext.Response.Cookies.Append(cOpts.name, t, policy)
                return {| ok = true |} |> JsonResult :> ActionResult
            | None -> return {| ok = false |} |> JsonResult :> ActionResult
        }
        |> Async.StartAsTask


    [<Route("")>]
    [<HttpHeader("STI-Logout")>]
    member self.Logout(headers: ForwardedHeaders) =
        async {
            let policy = cookiePolicy headers.host
            self.HttpContext.Response.Cookies.Delete(cOpts.name, policy)
            return {| ok = true |} |> JsonResult :> ActionResult
        }
        |> Async.StartAsTask
