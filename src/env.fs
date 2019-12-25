namespace STI


open DomainAgnostic
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open System
open Consts

module Env =

    type Variables =
        { logLevel: LogLevel
          port: int
          baseUri: PathString
          title: string
          background: string }


    let private prefix = sprintf "%s_%s" ENVPREFIX

    let private required name =
        let err =
            sprintf "\n\n\n-- MISSING ENVIRONMENTAL VARIABLE :: [%s] --\n\n\n" (prefix name)
        err |> Option.ForceUnwrap


    let private pLog find =
        find (prefix "LOG_LEVEL")
        |> Option.bind Parse.Enum<LogLevel>
        |> Option.Recover LogLevel.Warning

    let private pPort find =
        find (prefix "PORT")
        |> Option.bind Parse.Int
        |> Option.Recover WEBSRVPORT


    let private pBaseUri find =
        let parse = Result.New(fun (s: string) -> PathString("/" + s.Trim('/')))
        find (prefix "PATH_PREFIX")
        |> Option.bind (parse >> Option.FromResult)
        |> Option.Recover(PathString("/"))


    let private pBackground find = find (prefix "BACKGROUND") |> Option.Recover("background.png")


    let private pTitle find = find (prefix "TITLE") |> Option.Recover DEFAULTTITLE


    let Opts() =
        let find = ENV() |> flip Map.tryFind
        { logLevel = pLog find
          port = pPort find
          baseUri = pBaseUri find
          title = pTitle find
          background = pBackground find }
