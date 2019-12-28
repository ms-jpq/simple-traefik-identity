namespace STI.Views

open DomainAgnostic
open Giraffe.GiraffeViewEngine
open STI.Consts
open Layout

module Login =


    let private login =
        div []
            [ h1 [] []
              form
                  [ _action "sti-login"
                    _method "post" ]
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

    let Render background tit =
        let nodes = Layout background tit "" "" [ login ]
        nodes |> renderHtmlDocument
