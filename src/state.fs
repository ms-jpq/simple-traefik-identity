namespace STI


open DomainAgnostic
open System
open Consts

module State =
    type State =
        { history: Map<string, DateTime seq> }
