namespace STI.Views

open STI.Env
open DomainAgnostic
open Giraffe.GiraffeViewEngine
open Layout


module Unauthorized =

    let private denied goto = form [] [ h1 [] [ str "🚫" ] ]



    let Render (display: Display) goto =
        async {
            let! _js = "js/unauthorized.js"
                       |> Load display.resources
                       |> Async.StartChild
            let! _css = "css/unauthorized.css"
                        |> Load display.resources
                        |> Async.StartChild
            let! js = _js
            let! css = _css
            let nodes = Layout js css display.background display.title [ denied goto ]
            return nodes |> renderHtmlDocument
        }
