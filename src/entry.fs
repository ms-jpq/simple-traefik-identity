namespace STI

open DomainAgnostic
open Consts
open STI.State
open STI.Env
open DomainAgnostic.Globals
open Microsoft.Extensions.Hosting


module Entry =

    [<EntryPoint>]
    let main argv =
        echo README

        let deps = Opts()
        echo "🙆‍♀️ -- Options -- 🙆‍♀️"
        Variables.Desc deps |> echo

        use state = new GlobalVar<State>({ history = Map.empty })

        use server = Server.Build deps state
        server.Run()
        0
