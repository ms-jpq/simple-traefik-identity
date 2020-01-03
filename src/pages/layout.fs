namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open STI.Consts
open System.IO


module Layout =

    let _css = sprintf "body { background-image: url(%s); }"

    let load js css =
        (js, css)
        ||> sprintf """
        "use strict";
        (([script, style]) => {
          (async () => {
            script.textContent = await (await fetch(%s).text())
            document.head.append(script)
          })()
          ;(async () => {
            style.textContent = await (await fetch(%s).text())
            document.head.append(style)
          })()
        })(["script", "style"].map((t) => document.createElement(t)));
        """

    let Layout js css background tit form =
        html []
            [ head []
                  [ meta [ _charset "utf-8" ]
                    meta
                        [ _name "viewport"
                          _content "width=device-width, initial-scale=1" ]
                    script [] [ load js css |> rawText ]
                    style []
                        [ background
                          |> _css
                          |> str ]
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
