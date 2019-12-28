namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open Layout


module Logout =

    let private logout =
        form [ _action "" ]
            [ h1 [] [ str "Hasta la vista, baby" ]
              input [ _type "submit" ] ]


    let Render resources background tit =
        async {
            let! _js = "js/logout.js"
                       |> Load resources
                       |> Async.StartChild
            let! _css = "css/logout.css"
                        |> Load resources
                        |> Async.StartChild
            let! js = _js
            let! css = _css
            let nodes = Layout js css background tit [ logout ]
            return nodes |> renderHtmlDocument
        }
