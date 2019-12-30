namespace STI

open STI.Env
open STI.State
open System
open System.IO

module RateLimit =

    type Limit =
        | Umlimited
        | Limited of TimeSpan
