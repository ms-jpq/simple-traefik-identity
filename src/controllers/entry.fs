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

    [<Route("")>]
    member self.Index() =
        async {
            use stream = new StreamReader(self.HttpContext.Request.Body)

            let headers =
                self.HttpContext.Request.Headers
                |> Seq.cast<KeyValuePair<string, StringValues>>
                |> Seq.map (fun x -> x.Key, x.Value)
                |> Map.ofSeq
            let! body = stream.ReadToEndAsync() |> Async.AwaitTask

            headers
            |> Map.toSeq
            |> Seq.iter echo

            echo "\n\n\n"
            echo body

            return 200
        }
        |> Async.StartAsTask
