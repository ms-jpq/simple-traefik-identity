﻿namespace STI

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
        use state = new GlobalVar<State>({ dummy = 0 })

        use server = Server.Build deps state
        server.Run()
        0
