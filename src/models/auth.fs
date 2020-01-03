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


    let newToken jOpts model header =
        header
        |> decode
        |> Option.bind ((<||) (login model))
        |> Option.map (fun u -> { access = u.subDomains })
        |> Option.map (AccessClaims.Serialize >> (newJWT jOpts))


    let checkAuth jopts (domain: string) cookie =
        let state =
            maybe {
                let! claims = cookie |> readJWT jopts

                let! model = AccessClaims.DeSerialize claims
                let auth =
                    match model.access with
                    | All -> AuthState.Authorized
                    | Named domains ->
                        let contains = domains |> Seq.Contains(fun d -> domain.EndsWith(d))
                        match contains with
                        | true -> AuthState.Authorized
                        | false -> AuthState.Unauthorized
                return auth
            }
        state |> Option.Recover AuthState.Unauthenticated
