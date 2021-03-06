namespace STI.Models

open STI.Env
open DomainAgnostic
open DomainAgnostic.Encode
open JWT
open Newtonsoft.Json


module Auth =

    type AccessClaims =
        { user: string
          access: Domains }

        static member Serialize(claim: AccessClaims) = claim |> JsonConvert.SerializeObject

        static member DeSerialize claim =
            claim
            |> Result.New JsonConvert.DeserializeObject<AccessClaims>
            |> Option.OfResult

    type AuthState =
        | Unauthenticated
        | Unauthorized
        | Whitelisted
        | Authorized of string

    let private decode (header: string) =
        try
            let credentials = header.Split(" ")
            if credentials.[0] <> "Basic" then failwith "<> Basic"

            let decoded =
                credentials.[1]
                |> base64decode
                |> fun s -> s.Split(":")

            let username = decoded.[0]
            let password = decoded.[1]
            (username, password) |> Some

        with _ -> None


    let private login (model: AuthModel) username password =
        let seek (u: User) = u.name = username && u.password = password
        model.users |> Seq.tryFind seek

    let private tokenize opts (user: User) =
        let access =
            { user = user.name
              access = user.subDomains }
        AccessClaims.Serialize access |> newJWT opts user.loginSession


    let newToken opts model header =
        header
        |> decode
        |> Option.bind ((<||) (login model))
        |> Option.map (tokenize opts)


    let checkAuth opts model (domain: string) cookie =
        let contain = model.whitelist |> Seq.Contains domain.EndsWith

        let state =
            maybe {
                let! claims = cookie |> Option.bind (readJWT opts)
                let! acc = AccessClaims.DeSerialize claims
                let auth =
                    match acc.access with
                    | All -> Authorized acc.user
                    | Named domains ->
                        let contains = domains |> Seq.Contains domain.EndsWith
                        match contains with
                        | true -> Authorized acc.user
                        | false -> Unauthorized
                return auth
            }
        match contain with
        | true -> Some Whitelisted
        | false -> state
