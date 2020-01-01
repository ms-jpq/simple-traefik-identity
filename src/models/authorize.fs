namespace STI.Models

open STI.Env
open JWT
open DomainAgnostic
open System



module Authorize =

    type AuthState =
        | Unauthenticated = 407
        | Unauthorized = 403
        | Authorized = 203


    let checkAuth jopts (domain: string) cookie =
        let state =
            maybe {
                let! claims = cookie |> readJWT jopts

                let! model = JwtClaim.DeSerialize claims
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
