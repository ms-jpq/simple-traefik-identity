namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open STI.Consts


module Layout =

    let _css = sprintf "body { --background-image: url(%s); }"

    let load js css =
        (css, js)
        ||> sprintf """
        "use strict";
        (([style, script]) => {
          (async () => {
            style.textContent = await (await fetch("%s")).text()
            document.head.append(style)
          })();
          (async () => {
            script.textContent = await (await fetch("%s")).text()
            document.head.append(script)
          })();
        })(["style", "script"].map(t => document.createElement(t)));
        """


    let Layout js css background tit form =
        html []
            [ head []
                  [ meta [ _charset "utf-8" ]
                    meta
                        [ _name "viewport"
                          _content "width=device-width, initial-scale=1" ]
                    style [] [ background |> _css |> str ]
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
                    footer [] [ a [ _href PROJECTURI ] [ str "★ github ★" ] ]
                    div [] []
                    script [] [ load js css |> rawText ] ] ]
