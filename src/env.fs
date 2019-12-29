namespace STI


open DomainAgnostic
open Consts
open Legivel.Serialization
open Microsoft.Extensions.Logging
open System
open System.IO
open System.Text



module Env =

    type CookieOpts =
        { name: string
          maxAge: TimeSpan }

    type JWTopts =
        { secret: byte array
          issuer: string
          audience: string
          lifespan: TimeSpan }

    type Domains =
        | Named of string seq
        | All

    type User =
        { name: string
          password: string
          subDomains: Domains }

    type AuthModel =
        { domains: string seq
          users: User seq }

    type Variables =
        { logLevel: LogLevel
          port: int
          model: AuthModel
          cookie: CookieOpts
          jwt: JWTopts
          logoutUri: Uri option
          resources: string
          title: string
          background: string }

    type RawGroup =
        { name: string
          subDomains: string list }
        static member Identity a b = a.name = b.name

    type RawUser =
        { name: string
          password: string
          groups: string list }
        static member Identity a b = a.name = b.name

    type ConfYaml =
        { loglevel: string option
          port: int option
          secret: string option
          domains: string list option
          groups: RawGroup list option
          users: RawUser list option
          logoutUri: string option
          title: string option
          background: string option }


    let private prefix = sprintf "%s_%s" ENVPREFIX


    let private required name =
        let err =
            sprintf "\n\n\n-- MISSING ENVIRONMENTAL VARIABLE :: [%s] --\n\n\n" (prefix name)
        err |> Option.ForceUnwrap


    let private pYaml conf =
        let def =
            { loglevel = None
              port = None
              secret = None
              domains = None
              groups = None
              users = None
              logoutUri = None
              title = None
              background = None }

        let mapSucc res =
            match res with
            | Success s -> Some s.Data
            | Error _ -> None

        let parse yml =
            yml
            |> DeserializeWithOptions<ConfYaml> [ MappingMode(MapYaml.WithCrossCheck) ]
            |> Seq.choose mapSucc
            |> Seq.tryHead

        match File.Exists conf with
        | false -> None
        | true -> File.ReadAllText conf |> parse
        |> Option.defaultValue def


    let private pLog find =
        find (prefix "LOG_LEVEL")
        |> Option.bind Parse.Enum<LogLevel>
        |> Option.Recover LogLevel.Warning


    let private pPort find =
        find (prefix "PORT")
        |> Option.bind Parse.Int
        |> Option.Recover WEBSRVPORT


    let private pDomain find =
        find (prefix "DOMAINS")
        |> Option.map (fun (d: string) -> d.Split(";"))
        |> Option.map Seq.ofArray
        |> Option.defaultValue Seq.empty


    let private pSecret find = find (prefix "SECRET")

    let private pGroup (group: string) =
        match group.Split(":") |> List.ofArray with
        | [ name; domains ] ->
            { name = name
              subDomains = domains.Split(",") |> List.ofArray }
            |> Some
        | _ -> None

    let private pGroups find =
        find (prefix "GROUPS")
        |> Option.map (fun (g: string) -> g.Split(";"))
        |> Option.defaultValue [||]
        |> Seq.ofArray
        |> Seq.choose pGroup


    let private pUser (user: string) =
        match user.Split(":") |> List.ofArray with
        | [ name; password; groups ] ->
            { name = name
              password = password
              groups = groups.Split(",") |> List.ofArray }
            |> Some
        | _ -> None

    let private pUsers find =
        find (prefix "USERS")
        |> Option.map (fun (u: string) -> u.Split(";"))
        |> Option.defaultValue [||]
        |> Seq.ofArray
        |> Seq.choose pUser

    let private pLogout find = find (prefix "LOG_OUT")

    let private pBackground find = find (prefix "BACKGROUND") |> Option.Recover(BACKGROUND)

    let private pTitle find = find (prefix "TITLE") |> Option.Recover DEFAULTTITLE


    let private pmodel (groups: RawGroup seq) (users: RawUser seq) =
        let pDomain acc curr =
            match (acc, curr) with
            | _, "*" -> All
            | All, _ -> All
            | Named a, c -> a ++ [ c ] |> Named

        let mkUser (user: RawUser) =
            let chk =
                user.groups
                |> Set
                |> flip Set.contains

            let domains =
                groups
                |> Seq.filter (fun g -> chk g.name)
                |> Seq.Bind(fun g -> g.subDomains |> Seq.ofList)
                |> Seq.fold pDomain (Named Seq.empty)

            { name = user.name
              password = user.password
              subDomains = domains }

        users |> Seq.map mkUser


    let Opts() =
        let find = ENV() |> flip Map.tryFind
        let yaml = find (prefix "CONF_FILE") |> Option.defaultValue CONFFILE
        let ymlConf = pYaml yaml

        let log =
            ymlConf.loglevel
            |> Option.bind Parse.Enum<LogLevel>
            |> Option.defaultValue (pLog find)

        let port = ymlConf.port |> Option.defaultValue (pPort find)

        let g =
            ymlConf.groups
            |> Option.defaultValue []
            |> Seq.ofList
            |> (++) (pGroups find)

        let u =
            ymlConf.users
            |> Option.defaultValue []
            |> Seq.ofList
            |> (++) (pUsers find)

        let users = pmodel g u

        let domains =
            ymlConf.domains
            |> Option.defaultValue []
            |> Seq.ofList
            |> (++) (pDomain find)

        let secret =
            match (ymlConf.secret, pSecret find) with
            | Some s1, _ -> s1
            | _, Some s2 -> s2
            | _ -> sprintf "Did not find |SECRET| in either ENVIRONMENT or %s" yaml |> failwith
            |> Encoding.UTF8.GetBytes

        match secret.Length with
        | x when x < 128 -> failwith "Secret not long enough!"
        | _ -> ()

        let model =
            { domains = domains
              users = users }


        let cookie =
            { name = COOKIENAME
              maxAge = COOKIEMAXAGE }

        let jwt =
            { secret = secret
              issuer = TOKENISSUER
              audience = TOKENAUDIENCE
              lifespan = TOKENLIFESPAN }

        let logout =
            match (ymlConf.logoutUri, pLogout find) with
            | Some uri, _ -> Some uri
            | _, Some uri -> Some uri
            | _, _ -> None
            |> Option.bind Parse.Uri

        let title = ymlConf.title |> Option.defaultValue (pTitle find)

        let background = ymlConf.background |> Option.defaultValue (pBackground find)

        { logLevel = log
          port = port
          model = model
          cookie = cookie
          jwt = jwt
          logoutUri = logout
          resources = RESOURCESDIR
          title = title
          background = background }
