namespace STI


open DomainAgnostic
open System


module State =
    type State =
        { history: Map<string, DateTime seq> }
