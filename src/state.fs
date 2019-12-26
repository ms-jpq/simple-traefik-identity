namespace STI


open DomainAgnostic
open System
open Consts


module State =

    type Domains =
        | Named of string seq
        | All

    type User =
        { name: string
          password: string
          domains: Domains }

    type AuthModel =
        { secret: string
          users: User seq }
