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


    let Render(display: Display) =
        let js = "js/logout.js"
        let css = "css/logout.css"
        let nodes = Layout js css display.background display.title [ logout ]
        nodes |> renderHtmlDocument
