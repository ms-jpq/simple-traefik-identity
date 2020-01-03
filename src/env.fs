namespace STI


open DomainAgnostic
open Consts
open Microsoft.Extensions.Logging
open System
open Newtonsoft.Json
open Newtonsoft.Json.Converters
open Thoth.Json.Net
open YamlDotNet.Serialization
open System.Dynamic
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

                let maxAge =
                    get.Optional.Field "max_age" Decode.string
                    |> Option.bind Parse.Float
                    |> Option.map TimeSpan.FromHours
                    |> Option.Recover CookieOpts.Def.maxAge
                { name = name
                  maxAge = maxAge }

            Decode.object resolve


    type JWTopts =
        { [<JsonIgnore>]
          secret: byte array
          issuer: string }

        static member Decoder =
            let resolve (get: Decode.IGetters) =
                let secret = get.Required.Field "secret" Decode.string |> Encoding.UTF8.GetBytes

                match secret.Length with
                | l when l <= 150 -> failwith "☢️ -- PICK A LONGER SECRET -- ☢️"
                | _ -> ()

                { secret = secret
                  issuer = TOKENISSUER }

            Decode.object resolve


    type Domains =
        | Named of string seq
        | All

    type User =
        { name: string
          [<JsonIgnore>]
          password: string
          session: TimeSpan
          subDomains: Domains }

    type AuthModel =
        { baseDomains: string seq
          whitelist: string seq
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

                let session =
                    get.Optional.Field "session" Decode.string
                    |> Option.bind Parse.Float
                    |> Option.map TimeSpan.FromHours
                    |> Option.Recover TOKENLIFESPAN

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
                  session = session
                  subDomains = subDomains }

            let resolve (get: Decode.IGetters) =
                let baseDomains =
                    get.Optional.Field "base_domains" (Decode.list Decode.string)
                    |> Option.Recover []
                    |> Seq.ofList

                let whitelist =
                    get.Optional.Field "whitelist" (Decode.list Decode.string)
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
                  whitelist = whitelist
                  users = users }

            Decode.object resolve


    type RateLimit =
        { headers: string seq
          rate: int
          timer: TimeSpan }

        static member Def =
            { headers = []
              rate = RATE
              timer = RATETIMER }

        static member Decoder =
            let resolve (get: Decode.IGetters) =
                let headers =
                    get.Optional.Field "header" (Decode.array Decode.string)
                    |> Option.map Seq.ofArray
                    |> Option.Recover RateLimit.Def.headers

                let rate = get.Optional.Field "rate" Decode.int |> Option.Recover RateLimit.Def.rate

                let timer =
                    get.Optional.Field "timer" Decode.string
                    |> Option.bind Parse.Float
                    |> Option.map TimeSpan.FromSeconds
                    |> Option.Recover RateLimit.Def.timer
                { headers = headers
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
          rateLimit: RateLimit
          display: Display }

        static member Decoder =
            let resolve (get: Decode.IGetters) =

                let sys = get.Optional.Field "sys" SysOpts.Decoder |> Option.Recover SysOpts.Def
                let cookie = get.Optional.Field "cookie" CookieOpts.Decoder |> Option.Recover CookieOpts.Def
                let jwt = get.Required.Field "jwt" JWTopts.Decoder
                let model = get.Required.Field "auth" AuthModel.Decoder
                let rateLimit = get.Optional.Field "rate_limit" RateLimit.Decoder |> Option.Recover RateLimit.Def
                let display = get.Optional.Field "display" Display.Decoder |> Option.Recover Display.Def

                { sys = sys
                  cookie = cookie
                  jwt = jwt
                  model = model
                  rateLimit = rateLimit
                  display = display }
            Decode.object resolve

        static member Desc(v: Variables) =
            let json = JsonConvert.SerializeObject v
            let expanded = JsonConvert.DeserializeObject<ExpandoObject>(json, ExpandoObjectConverter())
            Serializer().Serialize(expanded)


    let Y2J(yaml: string) =
        yaml
        |> DeserializerBuilder().Build().Deserialize
        |> SerializerBuilder().JsonCompatible().Build().Serialize


    let Opts() =
        let conf =
            ENV()
            |> Map.tryFind APPCONF
            |> Option.Recover CONFFILE
        conf
        |> slurp
        |> Async.RunSynchronously
        |> Result.bind (Result.New Y2J)
        |> Result.mapError
            (conf
             |> sprintf "☢️ -- Failed to load config %s -- ☢️"
             |> constantly)
        |> Result.bind (Decode.fromString Variables.Decoder)
        |> Result.mapError Exception
        |> Result.ForceUnwrap
