namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open STI.Consts
open Layout

module Logout =

    let private logout =
        form
            [ _action ""
              _method "post" ]
            [ h1 [] [ str "Hasta la vista, baby" ]
              input [ _type "submit" ] ]


    let Render background tit =
        let nodes = Layout background tit "js/logout.js" "css/logout.css" [ logout ]
        nodes |> renderHtmlDocument
