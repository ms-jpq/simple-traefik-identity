namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open STI.Consts
open System.IO


module Layout =

    let _css = sprintf "body { background-image: url(%s); }"

    let Layout js css background tit form =
        html []
            [ head []
                  [ meta [ _charset "utf-8" ]
                    meta
                        [ _name "viewport"
                          _content "width=device-width, initial-scale=1" ]
                    style [] [ rawText css ]
                    style []
                        [ background
                          |> _css
                          |> str ]
                    script [] [ rawText js ]
                    title [] [ str tit ] ]
              body []
                  [ div [] []
                    main []
                        [ div []
                              [ div [] []
                                span [] []
                                div [] form
                                span [] []
                                div [] [] ] ]
                    div [] []
                    footer [] [ a [ _href PROJECTURI ] [ str "github" ] ]
                    div [] [] ] ]
