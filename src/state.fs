namespace STI


open DomainAgnostic
open System
open Consts


type State =
    { history: Map<string, DateTime seq> }
