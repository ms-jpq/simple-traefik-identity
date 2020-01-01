namespace STI.Models

open DomainAgnostic
open STI.Env
open STI.State
open System


module RateLimit =

    let next limit state ip =
        let now = DateTime.UtcNow
        let ago = now - limit.timer

        let hist =
            state.history
            |> Map.tryFind ip
            |> Option.Recover Seq.empty

        let go =
            hist
            |> Seq.Count((<) ago)
            |> (<) limit.rate

        let next =
            hist
            |> Seq.Appending now
            |> Seq.filter (fun d -> d > ago)
            |> flip (Map.add ip) state.history

        go, { history = next }
