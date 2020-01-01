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


    let checkAuth jopts (copts: CookieOpts) domain cookies =
        let state =
            maybe {
                let! claims = cookies
                              |> Map.tryFind copts.name
                              |> Option.bind (readJWT jopts)

                let! model = JwtClaim.DeSerialize claims
                let auth =
                    match model.access with
                    | All -> AuthState.Authorized
                    | Named lst ->
                        let chk =
                            lst
                            |> Set
                            |> flip Set.contains
                        match chk domain with
                        | true -> AuthState.Authorized
                        | false -> AuthState.Unauthorized
                return auth
            }
        state |> Option.Recover AuthState.Unauthenticated
