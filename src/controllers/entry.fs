namespace STI.Controllers

open STI.Env
open STI.State
open STI.Views.Login
open DomainAgnostic
open DomainAgnostic.Globals
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging


[<Route("")>]
type Entry(logger: ILogger<Entry>, deps: Container<Variables>, state: GlobalVar<State>) =
    inherit Controller()

    [<Route("")>]
    member self.Index() =
        async {
            let! s = state.Get()
            let html = Render deps.Boxed.background deps.Boxed.title s.routes.succ
            return self.Content(html, "text/html") :> ActionResult
        }
        |> Async.StartAsTask
