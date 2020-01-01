namespace STI.Models

open STI.Env
open DomainAgnostic
open JWT
open System
open System.Text


module Authenticate =

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


    let login (model: AuthModel) username password =
        let seek (u: User) = u.name = username && u.password = password
        model.users |> Seq.tryFind seek


    let newToken jOpts model header =
        header
        |> decode
        |> Option.bind ((<||) (login model))
        |> Option.map (fun u -> { access = u.subDomains })
        |> Option.map JwtClaim.Serialize
        |> Option.map (newJWT jOpts)
