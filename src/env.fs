namespace STI


open DomainAgnostic
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open System.IO
open Consts
open State
open Legivel.Serialization


module Env =

    type Variables =
        { logLevel: LogLevel
          port: int
          baseUri: PathString
          title: string
          background: string }

    type RawGroup =
        { name: string
          domains: string seq }

    type RawUser =
        { name: string
          password: string
          groups: string seq }

    type ConfYaml =
        { secret: string option
          groups: RawGroup seq option
          users: RawUser seq option }

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


    let private pYaml conf =
        let mapSucc res =
            match res with
            | Success s -> Some s.Data
            | Error _ -> None

        let parse yml =
            Deserialize<ConfYaml> yml
            |> Seq.choose mapSucc
            |> Seq.tryHead

        let yaml =
            match File.Exists conf with
            | false -> None
            | true -> File.ReadAllText conf |> parse

        match yaml with
        | None -> None, Seq.empty, Seq.empty
        | Some y ->
            let groups = y.groups |> Option.defaultValue Seq.empty
            let users = y.users |> Option.defaultValue Seq.empty
            y.secret, groups, users



    let private pGroup (group: string) =
        match group.Split(":") |> List.ofArray with
        | [ name; domains ] ->
            { name = name
              domains = domains.Split(",") |> Seq.ofArray }
            |> Some
        | _ -> None

    let private pUser (user: string) =
        match user.Split(":") |> List.ofArray with
        | [ name; password; groups ] ->
            { name = name
              password = password
              groups = groups.Split(",") |> Seq.ofArray }
            |> Some
        | _ -> None

    let private pConf (find: string -> string option) =
        let secret = find (prefix "SECRET")

        let groups =
            find (prefix "GROUPS")
            |> Option.map (fun g -> g.Split(";"))
            |> Option.defaultValue [||]
            |> Seq.ofArray
            |> Seq.choose pGroup

        let users =
            find (prefix "USERS")
            |> Option.map (fun u -> u.Split(";"))
            |> Option.defaultValue [||]
            |> Seq.ofArray
            |> Seq.choose pUser

        secret, groups, users

    let private model groups users = ()


    let private config find =
        let yaml = find (prefix "CONF_FILE") |> Option.defaultValue CONFFILE
        let s1, g1, u1 = pConf find
        let s2, g2, u2 = pYaml yaml
        let groups = g1 ++ g2
        let users = u1 ++ u2

        let secret =
            match (s1, s2) with
            | Some s1, _ -> s1
            | _, Some s2 -> s2
            | _ -> sprintf "Required |SECRET| in either %s or ENV - SECRET" yaml |> failwith

        let m = model groups users
        ()


    let Opts() =
        let find = ENV() |> flip Map.tryFind
        { logLevel = pLog find
          port = pPort find
          baseUri = pBaseUri find
          title = pTitle find
          background = pBackground find }
