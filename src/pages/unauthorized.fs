namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open Layout


module Unauthorized =

    let private denied goto = form [] [ h1 [] [ str "🚫" ] ]



    let Render resources background tit goto =
        async {
            let! _js = "js/unauthorized.js"
                       |> Load resources
                       |> Async.StartChild
            let! _css = "css/unauthorized.css"
                        |> Load resources
                        |> Async.StartChild
            let! js = _js
            let! css = _css
            let nodes = Layout js css background tit [ denied goto ]
            return nodes |> renderHtmlDocument
        }
