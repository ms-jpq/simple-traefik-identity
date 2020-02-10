namespace STI.Views

open STI.Env
open DomainAgnostic
open Giraffe.GiraffeViewEngine
open Layout


module Login =

    let private login =
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
                      span [] [] ] ]
          div [] [] ]


    let Render(display: Display) =
        let js = [ "/js/login.js" ]
        let css = [ "/css/base.css"; "/css/login.css" ]
        let nodes = Layout js css display.background display.title login
        nodes |> renderHtmlDocument
