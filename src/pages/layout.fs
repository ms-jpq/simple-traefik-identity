namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open STI.Consts


module Layout =

    let _css = sprintf "body { --background-image: url(%s); }"

    let loadContent file =
        sprintf "%s/%s" RESOURCESDIR file
        |> slurp
        |> Async.RunSynchronously
        |> Result.ForceUnwrap

    let loadJS js =
        js
        |> Seq.map (loadContent >> (fun c -> script [ _defer ] [ rawText c ]))
        |> Seq.toList

    let loadCSS css =
        css
        |> Seq.map (loadContent >> (fun c -> style [] [ rawText c ]))
        |> Seq.toList

    let Layout js css background tit form =
        let headElem =
            [ meta [ _charset "utf-8" ]
              meta
                  [ _name "viewport"
                    _content "width=device-width, initial-scale=1" ]
              title [] [ str tit ]
              style []
                  [ background
                    |> _css
                    |> str ] ]
        html []
            [ head [] (headElem @ loadJS js @ loadCSS css)
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
                    div [] [] ] ]
