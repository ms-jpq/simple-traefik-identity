namespace STI.Controllers

open STI
open STI.Env
open STI.Consts
open STI.Views.Login
open DomainAgnostic
open DomainAgnostic.Globals
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Authorization
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Primitives
open Microsoft.IdentityModel.Tokens
open System
open System.IO
open System.Text
open System.Collections.Generic
open System.IdentityModel.Tokens.Jwt


[<Route("")>]
type Entry(logger: ILogger<Entry>, deps: Container<Variables>, state: GlobalVar<State>) =
    inherit Controller()

    let cOpts = deps.Boxed.cookie
    let jOpts = deps.Boxed.jwt

    let credentials =
        let bytes = deps.Boxed.model.secret |> Encoding.UTF8.GetBytes
        let key = SymmetricSecurityKey(bytes)
        let algo = SecurityAlgorithms.HmacSha256Signature
        SigningCredentials(key, algo)


    let read (req: HttpRequest) =
        async {
            use stream = new StreamReader(req.Body)

            let headers =
                req.Headers
                |> Seq.cast<KeyValuePair<string, StringValues>>
                |> Seq.map (fun x -> x.Key, x.Value)
                |> Map.ofSeq

            let cookies =
                req.Cookies
                |> Seq.cast<KeyValuePair<string, string>>
                |> Seq.map (fun x -> x.Key, x.Value)
                |> Map.ofSeq

            let! body = stream.ReadToEndAsync() |> Async.AwaitTask
            return headers, cookies, body
        }


    let cookiePolicy domain =
        let policy = CookieOptions()
        policy.HttpOnly <- true
        policy.SameSite <- SameSiteMode.Strict
        policy.Secure <- cOpts.secure
        policy.MaxAge <- cOpts.maxAge |> Nullable
        policy.Domain <- cOpts.domain |> Option.defaultValue domain
        policy


    let newJTW claims =
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


    let validateJWT (token: string) =
        let validation = TokenValidationParameters()
        try
            let principal = JwtSecurityTokenHandler().ValidateToken(token, validation, ref null)
            principal.Claims
            |> Seq.map (fun c -> c.Type, c.Value)
            |> Map.ofSeq
            |> Some
        with _ -> None


    let checkAuth cookies =
        let auth =
            cookies
            |> Map.tryFind cOpts.name
            |> Option.bind validateJWT
        auth

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


    let authorize domain path (resp: HttpResponse) =
        let policy = cookiePolicy domain

        let token = newJTW Map.empty
        resp.Cookies.Append(cOpts.name, token, policy)

    let deauthorize domain path (resp: HttpResponse) =
        let policy = cookiePolicy domain
        resp.Cookies.Delete(cOpts.name, policy)


    [<Route("")>]
    member self.Index() =
        async {
            let! headers, cookies, body = read self.HttpContext.Request
            let domain, path = fdAuth headers
            let authStat = checkAuth cookies


            return self.Content("", "text/html") :> ActionResult
        }
        |> Async.StartAsTask
