namespace STI.Models

open STI.Env
open DomainAgnostic
open JWT
open Newtonsoft.Json
open System
open System.Text


module Auth =

    type AccessClaims =
        { access: Domains }

        static member Serialize(claim: AccessClaims) = claim |> JsonConvert.SerializeObject

        static member DeSerialize claim =
            claim
            |> Result.New JsonConvert.DeserializeObject<AccessClaims>
            |> Option.OfResult

    type AuthState =
        | Unauthenticated
        | Unauthorized
        | Authorized

    let decode (header: string) =
        try
            let credentials = header.Split(" ")
            if credentials.[0] <> "Basic" then failwith "..."

            let decoded =
                credentials.[1]
                |> Convert.FromBase64String
                |> Encoding.UTF8.GetString
                |> fun s -> s.Split(":")

            let username = decoded.[0]
            let password = decoded.[1]
            (username, password) |> Some

        with _ -> None


    let private login (model: AuthModel) username password =
        let seek (u: User) = u.name = username && u.password = password
        model.users |> Seq.tryFind seek

    let private tokenize opts (user: User) =
        let access = { access = user.subDomains }
        AccessClaims.Serialize access |> newJWT opts user.session


    let newToken opts model header =
        header
        |> decode
        |> Option.bind ((<||) (login model))
        |> Option.map (tokenize opts)


    let checkAuth opts (domain: string) cookie =
        let state =
            maybe {
                let! claims = cookie |> readJWT opts

                let! model = AccessClaims.DeSerialize claims
                let auth =
                    match model.access with
                    | All -> Authorized
                    | Named domains ->
                        let contains = domains |> Seq.Contains(fun d -> domain.EndsWith(d))
                        match contains with
                        | true -> Authorized
                        | false -> Unauthorized
                return auth
            }
        state |> Option.Recover Unauthenticated
