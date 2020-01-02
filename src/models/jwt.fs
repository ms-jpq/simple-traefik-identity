namespace STI.Models

open STI.Env
open DomainAgnostic
open Microsoft.IdentityModel.Tokens
open Newtonsoft.Json
open System
open System.IdentityModel.Tokens.Jwt


module JWT =

    type JwtClaim =
        { access: Domains }

        static member Serialize(claim: JwtClaim) = claim |> JsonConvert.SerializeObject

        static member DeSerialize claim =
            claim
            |> Result.New JsonConvert.DeserializeObject<JwtClaim>
            |> Option.OfResult

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
            |> Map.tryFind "payload"
            |> Option.bind CAST<string>
        with _ -> None


    let newJWT (opts: JWTopts) (payload: string) =
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
        assert (token.Payload.TryAdd("payload", payload))
        jwt.WriteToken token
