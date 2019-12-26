namespace STI


open DomainAgnostic
open System
open Consts


module State =

    type User =
        { name: string
          password: string }

    type Domain =
        { name: string
          users: User Set }

    type AuthModel =
        { secret: string
          domains: Domain Set }
