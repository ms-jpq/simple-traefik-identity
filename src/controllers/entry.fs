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
open System
open System.IO
open System.Collections.Generic


[<Route("")>]
type Entry(logger: ILogger<Entry>, deps: Container<Variables>, state: GlobalVar<State>) =
    inherit Controller()

    let cOpts = deps.Boxed.cookie

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
        policy.MaxAge <- Nullable cOpts.maxAge
        policy.Domain <- cOpts.domain |> Option.defaultValue domain
        policy

    let checkAuth cookies =
        let auth = Map.tryFind cOpts.name cookies
        auth

    let fdAuth headers =
        let err = "Missing Required Headers :: X-Forwarded-Host, X-Forwarded-Uri"
        maybe {
            let! domain = Map.tryFind "X-Forwarded-Host" headers
            let! path = Map.tryFind "X-Forwarded-Uri" headers
            return domain, path } |> Option.ForceUnwrap err


    let authorize domain path (resp: HttpResponse) =
        let policy = cookiePolicy domain
        let authContent = ""

        resp.Cookies.Append(cOpts.name, authContent, policy)

    let deauthorize domain path (resp: HttpResponse) =
        let policy = cookiePolicy domain
        resp.Cookies.Delete(cOpts.name, policy)


    [<Route("")>]
    member self.Index() =
        async {
            let! headers, cookies, body = read self.HttpContext.Request
            let domain, path = fdAuth headers
            let authStat = checkAuth cookies


            echo "\n\n\n"

            return self.Content("", "text/html") :> ActionResult
        }
        |> Async.StartAsTask
