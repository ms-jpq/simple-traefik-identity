namespace STI

open DomainAgnostic
open Consts
open STI.Env
open DomainAgnostic.Globals
open Microsoft.Extensions.Hosting

module Entry =

    [<EntryPoint>]
    let main argv =
        echo README

        let deps = Opts()

        use server = Server.Build deps
        server.Run()
        0
