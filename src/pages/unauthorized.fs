namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open STI.Consts
open Layout

module Unauthorized =

    let private denied = div [] [ h1 [] [ str "🚫" ] ]


    let Render background tit =
        let nodes = Layout background tit "js/unauthorized.js" "css/unauthorized.css" [ denied ]
        nodes |> renderHtmlDocument
