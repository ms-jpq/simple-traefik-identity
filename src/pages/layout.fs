namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open STI.Consts
open System.IO


module Layout =

    let _css = sprintf "body { background-image: url(%s); }"

    let Load resources resource =
        resource
        |> sprintf "%s%s" resources
        |> echo
        resource
        |> sprintf "%s%s" resources
        |> File.ReadAllTextAsync
        |> Async.AwaitTask

    let Layout js css background tit contents =
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
                    script [ _async; _defer ] [ rawText js ]
                    title [] [ str tit ] ]
              body []
                  [ main [] contents
                    footer [] [ a [ _href PROJECTURI ] [ str "github" ] ] ] ]
