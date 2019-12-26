namespace STI


open DomainAgnostic
open System
open Consts

module State =

    type Group =
        { name: string
          domains: Uri seq }

    type User =
        { name: string
          password: string
          groups: Group seq }
