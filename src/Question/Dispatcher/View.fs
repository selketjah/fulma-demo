module Question.Dispatcher.View

open Types
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fulma.Layouts
open Fulma.Elements
open Fulma.BulmaClasses

let root model dispatch =
    match model with
    | { CurrentPage = Router.QuestionPage.Index
        IndexModel = Some extractedModel } -> Question.Index.View.root extractedModel (IndexMsg >> dispatch)
    | { CurrentPage = Router.QuestionPage.Show _
        ShowModel = Some extractedModel } -> Question.Show.View.root extractedModel (ShowMsg >> dispatch)
    | _ ->
        Hero.hero [ Hero.isFullHeight
                    Hero.isDanger ]
            [ Hero.body [ ]
                [ Container.container [ Container.customClass Bulma.Properties.Alignment.HasTextCentered ]
                    [ Heading.h1 [ ]
                        [ str "404" ] ] ] ]