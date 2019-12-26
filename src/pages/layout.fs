namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open STI.Consts


module Layout =

    let _css = sprintf "body { background-image: url(%s); }"

    let Layout background tit js css contents =
        html []
            [ head []
                  [ meta [ _charset "utf-8" ]
                    meta
                        [ _name "viewport"
                          _content "width=device-width, initial-scale=1" ]
                    link
                        [ _rel "stylesheet"
                          _href css ]
                    style []
                        [ background
                          |> _css
                          |> str ]
                    script [ _src js ] []
                    title [] [ str tit ] ]
              body []
                  [ main [] contents
                    footer [] [ a [ _href PROJECTURI ] [ str "github" ] ] ] ]
