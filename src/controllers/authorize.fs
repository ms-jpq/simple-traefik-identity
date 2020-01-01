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


module Ingress =

    [<CLIMutable>]
    type LoginHeaders =
        { [<FromHeader(Name = "STI-Authorization")>]
          authorization: string }


open Ingress



[<Controller>]
[<Port(AUTHSRVPORT)>]
type Entry(logger: ILogger<Entry>, deps: Container<Variables>) =
    inherit Controller()


    let cname = deps.Boxed.cookie.name

    let redirect (req: HttpRequest) (resp: HttpResponse) reason =
        resp.StatusCode <- StatusCodes.Status307TemporaryRedirect
        let uri = req.GetEncodedUrl() |> base64encode

        let args =
            [ "redirect-uri", uri
              "redirect-reason", reason ]
            |> Map.ofSeq
            |> Map.ToKVP

        let query = QueryString.Create(args).ToString()
        let headers = [ "Location", query ]
        Exts.AddHeaders headers resp

    [<HttpGet("")>]
    member self.Index() =
        async {
            let req = self.HttpContext.Request
            let resp = self.HttpContext.Response
            let domain = req.Host |> string
            let _, cookies = Exts.Metadata req

            let authStatus =
                cookies
                |> Map.tryFind cname
                |> Option.Recover ""
                |> checkAuth deps.Boxed.jwt domain

            match authStatus with
            | AuthState.Authorized -> resp.StatusCode <- StatusCodes.Status200OK
            | AuthState.Unauthorized ->
                AuthState.Unauthorized
                |> string
                |> redirect req resp
            | AuthState.Unauthenticated
            | _ ->
                AuthState.Unauthenticated
                |> string
                |> redirect req resp
        }
        |> Async.StartAsTask


    [<HttpGet("_sti_auth")>]
    member self.Auth() = async { return 0 } |> Async.StartAsTask
