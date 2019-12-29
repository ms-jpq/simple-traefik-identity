namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open Layout


module Login =


    let private login goto =
        [ div [] []
          form [ _action "" ]
              [ div []
                    [ span [] []
                      figure []
                          [ div [] []
                            div [] [] ]
                      section []
                          [ div []
                                [ span [] []
                                  input
                                      [ _type "text"
                                        _name "username" ] ]
                            div []
                                [ span [] []
                                  input
                                      [ _type "password"
                                        _name "password" ] ]
                            div []
                                [ span [] []
                                  input
                                      [ _type "submit"
                                        _value " " ] ] ]
                      span [] [] ]
                div [] [ output [ _name "goto" ] [ rawText goto ] ] ]
          div [] [] ]

    let Render resources background tit goto =
        async {
            let! _js = "js/login.js"
                       |> Load resources
                       |> Async.StartChild
            let! _css = "css/login.css"
                        |> Load resources
                        |> Async.StartChild
            let! js = _js
            let! css = _css
            let nodes = Layout js css background tit (login goto)
            return nodes |> renderHtmlDocument
        }
