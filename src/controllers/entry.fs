namespace STI.Controllers

open STI.Env
open STI.State
open STI.Views.Login
open DomainAgnostic
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging


[<Route("")>]
type Entry(logger: ILogger<Entry>, deps: Container<Variables>) =
    inherit Controller()

    [<Route("")>]
    member self.Index() =
        async {
            let ctx = self.HttpContext
            echo ctx.Request.Headers

            let html = Render deps.Boxed.background deps.Boxed.title ""
            return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask
