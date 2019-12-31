namespace STI

open DomainAgnostic
open STI.Env
open STI.State
open System
open System.IO

module RateLimit =

    type Limit =
        | Umlimited
        | Limited of TimeSpan


    let auth (limit: RateLimit) (state: State) ip (now: DateTime) =
        let ago = now - limit.timer
        state.history
        |> Map.tryFind ip
        |> Option.defaultValue Seq.empty
        |> Seq.Count((<=) ago)
        |> (>=) limit.rate

    let update (state: State) ip now =
        let hist =
            state.history
            |> Map.tryFind ip
            |> Option.defaultValue Seq.empty
            |> Seq.Appending now
            |> flip (Map.add ip) state.history
        { history = hist }


    let next limit state ip =
        let now = DateTime.UtcNow
        let res = auth limit state ip now
        let next = update state ip now
        res, next
