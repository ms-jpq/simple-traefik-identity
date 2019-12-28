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

    let TEAPOT = 418

    type AuthState =
        | Unauthenticated = 407
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
        let key = authModel.secret |> SymmetricSecurityKey
        let algo = SecurityAlgorithms.HmacSha256Signature
        SigningCredentials(key, algo)


    let validateJWT (token: string) =
        let validation =
            let desc = TokenValidationParameters()
            desc.IssuerSigningKey <- credentials.Key
            desc.ValidIssuer <- jOpts.issuer
            desc.ValidAudience <- jOpts.audience
            desc.ValidateAudience <- false
            desc
        try
            let principal = JwtSecurityTokenHandler().ValidateToken(token, validation, ref null)
            principal.Claims
            |> Seq.map (fun c -> c.Type, c.Value)
            |> Map.ofSeq
            |> Some
        with e ->
            logger.LogError(e, "")
            None

    let newJWT claims =
        let now = DateTime.UtcNow

        let desc =
            let desc = SecurityTokenDescriptor()
            desc.SigningCredentials <- credentials
            desc.Issuer <- jOpts.issuer
            desc.Audience <- jOpts.audience
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


    [<HttpGet("")>]
    member self.Index(headers: ForwardedHeaders) =
        async {
            let resp = self.HttpContext.Response
            let _, cookies = Exts.Metadata self.HttpContext.Request
            let authState = checkAuth headers.host cookies

            let html, respHeaders =
                match authState with
                | AuthState.Authorized -> "", Seq.empty
                | AuthState.Unauthorized -> renderReq ||> Unauthorized.Render, Seq.empty
                | AuthState.Unauthenticated
                | _ -> renderReq ||> Login.Render, Seq.empty

            Exts.AddHeaders respHeaders resp
            resp.StatusCode <- authState |> LanguagePrimitives.EnumToValue
            return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask


    [<HttpGet("")>]
    [<HttpHeader("STI-Authorization")>]
    member self.Login(headers: ForwardedHeaders, credentials: LoginHeaders) =
        async {
            let resp = self.HttpContext.Response
            let uri = headers |> ForwardedHeaders.OriginalUri

            let token =
                credentials
                |> LoginHeaders.Decode
                |> Option.bind ((<||) login)
                |> Option.map (fun u -> { access = u.domains })
                |> Option.map (JwtClaim.Serialize >> newJWT)

            match token with
            | Some t ->
                let info =
                    sprintf "🦄 -- Authenticated -- 🦄\n%A" uri
                logger.LogWarning info
                let policy = cookiePolicy headers.host
                resp.Cookies.Append(cOpts.name, t, policy)
                resp.StatusCode <- TEAPOT
                return {| ok = true |} |> JsonResult :> ActionResult
            | None ->
                let info =
                    sprintf "⛔️ -- Authentication Attempt -- ⛔️\n%A" uri
                logger.LogWarning info
                return {| ok = false |} |> JsonResult :> ActionResult
        }
        |> Async.StartAsTask


    [<HttpGet("")>]
    [<HttpHeader("STI-Deauthorization")>]
    member self.Logout(headers: ForwardedHeaders) =
        async {
            let uri = headers |> ForwardedHeaders.OriginalUri

            let info =
                sprintf "👋 -- Deauthenticated -- 👋\n%A" uri

            let resp = self.HttpContext.Response
            let policy = cookiePolicy headers.host
            resp.Cookies.Delete(cOpts.name, policy)
            resp.StatusCode <- TEAPOT
            logger.LogWarning info
            return {| ok = true |} |> JsonResult :> ActionResult
        }
        |> Async.StartAsTask
