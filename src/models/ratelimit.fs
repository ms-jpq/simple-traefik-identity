namespace STI.Models

open DomainAgnostic
open STI.Env
open System


module RateLimit =

    let next limit history ip =
        let now = DateTime.UtcNow
        let ago = now - limit.timer

        let hist =
            history
            |> Map.tryFind ip
            |> Option.Recover Seq.empty

        let go =
            hist
            |> Seq.Count((<) ago)
            |> (>=) limit.rate

        let next =
            hist
            |> Seq.Appending now
            |> Seq.filter ((<) ago)
            |> flip (Map.add ip) history

        go, next
