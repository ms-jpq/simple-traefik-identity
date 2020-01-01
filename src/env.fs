namespace STI


open DomainAgnostic
open Consts
open Microsoft.Extensions.Logging
open System
open Thoth.Json.Net
open YamlDotNet.Serialization
open System.Text


module Env =

    type SysOpts =
        { logLevel: LogLevel
          port: int }

        static member Def =
            { logLevel = LogLevel.Warning
              port = WEBSRVPORT }

        static member Decoder =
            let resolve (get: Decode.IGetters) =
                let logLevel =
                    get.Optional.Field "log_level" Decode.string
                    |> Option.bind Parse.Enum<LogLevel>
                    |> Option.Recover SysOpts.Def.logLevel

                let port = get.Optional.Field "port" Decode.int |> Option.Recover SysOpts.Def.port
                { logLevel = logLevel
                  port = port }

            Decode.object resolve


    type CookieOpts =
        { name: string
          maxAge: TimeSpan }

        static member Def =
            { name = COOKIENAME
              maxAge = COOKIEMAXAGE }

        static member Decoder =
            let resolve (get: Decode.IGetters) =
                let name = get.Optional.Field "name" Decode.string |> Option.Recover CookieOpts.Def.name

                let maxAge = get.Optional.Field "max_age" Decode.timespan |> Option.Recover CookieOpts.Def.maxAge
                { name = name
                  maxAge = maxAge }

            Decode.object resolve


    type JWTopts =
        { secret: byte array
          lifespan: TimeSpan
          issuer: string }

        static member Decoder =
            let resolve (get: Decode.IGetters) =
                let secret = get.Required.Field "secret" Decode.string |> Encoding.UTF8.GetBytes
                let lifespan = get.Optional.Field "life_span" Decode.timespan |> Option.Recover TOKENLIFESPAN

                match secret.Length with
                | l when l <= 150 -> failwith "☢️ -- PICK A LONGER SECRET -- ☢️"
                | _ -> ()

                { secret = secret
                  lifespan = lifespan
                  issuer = TOKENISSUER }

            Decode.object resolve


    type Domains =
        | Named of string seq
        | All

    type User =
        { name: string
          password: string
          subDomains: Domains }

    type AuthModel =
        { baseDomains: string seq
          users: User seq }

        static member Decoder =
            let pDomain acc curr =
                match (acc, curr) with
                | _, "*" -> All
                | All, _ -> All
                | Named a, c -> a ++ [ c ] |> Named

            let resovleG (get: Decode.IGetters) =
                let name = get.Required.Field "name" Decode.string

                let subDomains = get.Required.Field "sub_domains" (Decode.list Decode.string) |> Seq.ofList

                name, subDomains

            let resovleU (groups: (string * string seq) seq) (get: Decode.IGetters) =
                let name = get.Required.Field "name" Decode.string
                let password = get.Required.Field "password" Decode.string

                let chk =
                    get.Required.Field "groups" (Decode.list Decode.string)
                    |> Set
                    |> flip Set.contains

                let subDomains =
                    groups
                    |> Seq.filter (fst >> chk)
                    |> Seq.Bind snd
                    |> Seq.fold pDomain (Named Seq.empty)

                { name = name
                  password = password
                  subDomains = subDomains }

            let resolve (get: Decode.IGetters) =
                let baseDomains =
                    get.Optional.Field "base_domains" (Decode.list Decode.string)
                    |> Option.Recover []
                    |> Seq.ofList

                let groups =
                    get.Required.Field "groups"
                        (resovleG
                         |> Decode.object
                         |> Decode.list) |> Seq.ofList
                let users =
                    get.Required.Field "users"
                        (groups
                         |> resovleU
                         |> Decode.object
                         |> Decode.list) |> Seq.ofList
                { baseDomains = baseDomains
                  users = users }

            Decode.object resolve


    type RateLimit =
        { header: string
          rate: int
          timer: TimeSpan }

        static member Def =
            { header = REMOTEADDR
              rate = RATE
              timer = RATETIMER }

        static member Decoder =
            let resolve (get: Decode.IGetters) =
                let header = get.Optional.Field "header" Decode.string |> Option.Recover RateLimit.Def.header
                let rate = get.Optional.Field "rate" Decode.int |> Option.Recover RateLimit.Def.rate
                let timer = get.Optional.Field "timer" Decode.timespan |> Option.Recover RateLimit.Def.timer
                { header = header
                  rate = rate
                  timer = timer }

            Decode.object resolve


    type Display =
        { resources: string
          title: string
          background: string }

        static member Def =
            { resources = RESOURCESDIR
              title = DEFAULTTITLE
              background = BACKGROUND }

        static member Decoder =
            let resolve (get: Decode.IGetters) =
                let title = get.Optional.Field "title" Decode.string |> Option.Recover Display.Def.title
                let background =
                    get.Optional.Field "background" Decode.string |> Option.Recover Display.Def.background
                { resources = Display.Def.resources
                  title = title
                  background = background }

            Decode.object resolve


    type Variables =
        { sys: SysOpts
          cookie: CookieOpts
          jwt: JWTopts
          model: AuthModel
          logoutUri: Uri
          rateLimit: RateLimit
          display: Display }

        static member Decoder =
            let resolve (get: Decode.IGetters) =
                let sys = get.Optional.Field "sys" SysOpts.Decoder |> Option.Recover SysOpts.Def
                let cookie = get.Optional.Field "cookie" CookieOpts.Decoder |> Option.Recover CookieOpts.Def
                let jwt = get.Required.Field "jwt" JWTopts.Decoder
                let model = get.Required.Field "auth" AuthModel.Decoder

                let logoutUri =
                    get.Optional.Field "logout_uri" Decode.string
                    |> Option.bind Parse.Uri
                    |> Option.Recover(Uri("about:blank"))

                let rateLimit = get.Optional.Field "rate_limit" RateLimit.Decoder |> Option.Recover RateLimit.Def
                let display = get.Optional.Field "display" Display.Decoder |> Option.Recover Display.Def

                { sys = sys
                  cookie = cookie
                  jwt = jwt
                  model = model
                  logoutUri = logoutUri
                  rateLimit = rateLimit
                  display = display }
            Decode.object resolve



    let Y2J(yaml: string) =
        yaml
        |> DeserializerBuilder().Build().Deserialize
        |> SerializerBuilder().JsonCompatible().Build().Serialize


    let Opts() =
        ENV()
        |> Map.tryFind APPCONF
        |> Option.Recover CONFFILE
        |> slurp
        |> Async.RunSynchronously
        |> Result.bind (Result.New Y2J)
        |> Result.mapError (constantly "☢️ -- Unable to load config -- ☢️")
        |> Result.bind (Decode.fromString Variables.Decoder)
        |> Result.mapError (constantly "☢️ -- Unable to parse config -- ☢️")
        |> Result.mapError Exception
        |> Result.ForceUnwrap
