namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open Layout


module Login =


    let private login =
        div []
            [ h1 [] []
              form []
                  [ div []
                        [ span [] [ str "🧕🏻" ]
                          input
                              [ _type "text"
                                _name "username" ] ]
                    div []
                        [ span [] [ str "🗝️" ]
                          input
                              [ _type "password"
                                _name "password" ] ]
                    input [ _type "submit" ] ] ]

    let Render resources background tit =
        async {
            let! _js = "js/login.js"
                       |> Load resources
                       |> Async.StartChild
            let! _css = "css/login.css"
                        |> Load resources
                        |> Async.StartChild
            let! js = _js
            let! css = _css
            let nodes = Layout js css background tit [ login ]
            return nodes |> renderHtmlDocument
        }
