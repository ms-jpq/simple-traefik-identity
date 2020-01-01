namespace STI

open STI.Env
open DomainAgnostic
open Microsoft.IdentityModel.Tokens
open System
open System.IdentityModel.Tokens.Jwt


module Auth =

    type AuthState =
        | Unauthenticated = 407
        | Unauthorized = 403
        | Authorized = 203

    type JwtClaim =
        { access: Domains }

        static member Serialize claim =
            match claim.access with
            | All -> [ "access", "*" ] |> Map.ofSeq
            | Named lst ->
                let access = String.Join(",", lst |> Seq.toArray)
                [ "access", access ] |> Map.ofSeq

        static member DeSerialize claim =
            maybe {
                let! access = claim |> Map.tryFind "access"
                let! res = match CAST<string> access with
                           | Some "*" -> Some { access = All }
                           | Some c ->
                               let a =
                                   c.Split(",")
                                   |> Seq.ofArray
                                   |> Named
                               Some { access = a }
                           | None -> None
                return res
            }


    let readJWT (opts: JWTopts) (token: string) =
        let jwt = JwtSecurityTokenHandler()

        let validation =
            let desc = TokenValidationParameters()
            desc.IssuerSigningKey <- opts.secret |> SymmetricSecurityKey
            desc.ValidIssuer <- opts.issuer
            desc.ValidateAudience <- false
            desc
        try
            jwt.ValidateToken(token, validation, ref null) |> ignore
            jwt.ReadJwtToken(token).Payload
            |> Map.OfKVP
            |> Some
        with _ -> None


    let newJWT (opts: JWTopts) claims =
        let jwt = JwtSecurityTokenHandler()
        let now = DateTime.UtcNow
        let key = opts.secret |> SymmetricSecurityKey
        let algo = SecurityAlgorithms.HmacSha256Signature

        let desc =
            let desc = SecurityTokenDescriptor()
            desc.SigningCredentials <- SigningCredentials(key, algo)
            desc.Issuer <- opts.issuer
            desc.IssuedAt <- now |> Nullable
            desc.Expires <- now + opts.lifespan |> Nullable
            desc

        let token = jwt.CreateJwtSecurityToken(desc)
        claims
        |> Map.toSeq
        |> Seq.iter token.Payload.Add
        jwt.WriteToken token


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
