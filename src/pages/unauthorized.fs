namespace STI.Views

open STI.Env
open DomainAgnostic
open Giraffe.GiraffeViewEngine
open Layout


module Unauthorized =

    let private denied goto = form [] [ h1 [] [ str "🚫" ] ]



    let Render (display: Display) goto =
        let js = "js/unauthorized.js"
        let css = "css/unauthorized.css"
        let nodes = Layout js css display.background display.title [ denied goto ]
        nodes |> renderHtmlDocument
