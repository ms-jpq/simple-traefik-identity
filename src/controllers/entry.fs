namespace STI.Controllers

open STI.Env
open STI.State
open STI.Views.Login
open DomainAgnostic
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open System.IO
open System.Collections.Generic
open Microsoft.Extensions.Primitives

[<Route("")>]
type Entry(logger: ILogger<Entry>, deps: Container<Variables>) =
    inherit Controller()

    let checkAuth cookies domain path =
        ()
        true

    let authorize domain path =
        ()
        ()

    let fdAuth headers =
        maybe {
            let! domain = Map.tryFind "X-Forwarded-Host" headers
            let! path = Map.tryFind "X-Forwarded-Uri" headers
            return domain, path }


    member private self.Read() =
        async {
            let ctx = self.HttpContext.Request
            use stream = new StreamReader(ctx.Body)

            let headers =
                ctx.Headers
                |> Seq.cast<KeyValuePair<string, StringValues>>
                |> Seq.map (fun x -> x.Key, x.Value)
                |> Map.ofSeq

            let cookies =
                ctx.Cookies
                |> Seq.cast<KeyValuePair<string, string>>
                |> Seq.map (fun x -> x.Key, x.Value)
                |> Map.ofSeq

            let! body = stream.ReadToEndAsync() |> Async.AwaitTask
            return headers, cookies, body
        }


    [<Route("")>]
    member self.Index() =
        async {
            let! headers, cookies, body = self.Read()
            let domain, path = fdAuth headers |> Option.ForceUnwrap ""


            echo "\n\n\n"

            return self.Content("", "text/html") :> ActionResult
        }
        |> Async.StartAsTask
