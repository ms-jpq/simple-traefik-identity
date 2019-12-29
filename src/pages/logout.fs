namespace STI.Views

open STI.Env
open DomainAgnostic
open Giraffe.GiraffeViewEngine
open Layout


module Logout =

    let private logout =
        form [ _action "" ]
            [ input
                [ _type "submit"
                  _value " " ] ]


    let Render (display: Display) =
        async {
            let! _js = "js/logout.js"
                       |> Load display.resources
                       |> Async.StartChild
            let! _css = "css/logout.css"
                        |> Load display.resources
                        |> Async.StartChild
            let! js = _js
            let! css = _css
            let nodes = Layout js css display.background display.title [ logout ]
            return nodes |> renderHtmlDocument
        }
