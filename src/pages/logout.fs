namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open STI.Consts
open Layout

module Logout =

    let private logout =
        form
            [ _action "/logout"
              _method "post" ]
            [ h1 [] [ str "Hasta la vista, baby" ]
              input [ _type "submit" ] ]


    let Render background tit =
        let nodes = Layout background tit "" "" [ logout ]
        nodes |> renderHtmlDocument
