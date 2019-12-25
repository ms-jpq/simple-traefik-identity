namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open STI.Consts


module Denied =

    let css = sprintf "body { background-image: url(%s); }"

    let Page background tit bdy =
        html []
            [ head []
                  [ meta [ _charset "utf-8" ]
                    meta
                        [ _name "viewport"
                          _content "width=device-width, initial-scale=1" ]
                    link
                        [ _rel "stylesheet"
                          _href "site.css" ]
                    style []
                        [ background
                          |> css
                          |> str ]
                    script [ _src "script.js" ] []
                    title [] [ str tit ] ]
              body [] bdy ]

    let Layout contents =
        div []
            [ main [] contents
              footer []
                  [ a [ _href "status" ] [ str "API" ]
                    a [ _href PROJECTURI ] [ str "github" ] ] ]
        |> List.singleton

    let Render background tit fff =
        ""
