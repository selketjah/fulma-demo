namespace Page.Question.Show

open Fable.Import
module Component =

    open Data.Forum
    open Data.User
    open Fable.PowerPack
    open Elmish
    module Answer = Answer.Component

    type Model =
        { Question : Question
          Answers : Answer.Model list
          Reply : string
          Error : string
          IsWaitingReply : bool }

    type Msg =
        | ChangeReply of string
        | Submit
        | CreateAnswerSuccess of Answer
        | CreateAnswerError of exn
        | AnswerMsg of int * Answer.Msg

    let init id =
        Requests.Question.getDetails id
        |> Promise.map (fun (question, answers) ->
            { Question = question
              Answers = Array.map (Answer.init id) answers |> Array.toList
              Reply = ""
              Error = ""
              IsWaitingReply = false } |> Ok
        )
        |> Promise.catch(fun error ->
            Error error.Message
        )

    let update currentUser msg (model: Model) =
        match msg with
        | ChangeReply value ->
            { model with Reply = value }, Cmd.none

        | Submit ->
            if model.IsWaitingReply then
                model, Cmd.none
            else
                if model.Reply <> "" then
                    { model with IsWaitingReply = true
                                 Error = "" }, Cmd.ofPromise
                                                    Requests.Answer.createAnswer
                                                    (model.Question.Id, currentUser.Id, model.Reply)
                                                    CreateAnswerSuccess
                                                    CreateAnswerError
                else
                    { model with Error = "Your answer can't be empty" }, Cmd.none

        | CreateAnswerSuccess data ->
            let answer = Answer.init model.Question.Id data
            { model with IsWaitingReply = false
                         Error = ""
                         Reply = ""
                         Answers = model.Answers @ [ answer ] }, Cmd.none

        | CreateAnswerError error ->
            Browser.console.log("An error occured when creating an answer: " + error.Message)
            { model with IsWaitingReply = false
                         Error = "An error occured, please try again" }, Cmd.none

        | AnswerMsg (refIndex, msg) ->
            let mutable newCmd = Cmd.none
            let newAnswers =
                model.Answers
                |> List.mapi(fun index answer ->
                    if index = refIndex then
                        let (subModel, subCmd) = Answer.update msg answer
                        newCmd <- Cmd.map (fun x -> AnswerMsg (index, x)) subCmd
                        subModel
                    else
                        answer
                )

            { model with Answers = newAnswers }, newCmd

    open Fulma.Elements
    open Fulma.Elements.Form
    open Fulma.Components
    open Fulma.Layouts
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import
    open Fable.Core.JsInterop

    let replyView (currentUser : User) model dispatch =
        Media.media [ ]
            [ Media.left [ ]
                [ Image.image [ Image.is64x64 ]
                    [ img [ Src ("avatars/" + currentUser.Avatar) ] ] ]
              Media.content [ ]
                [ Field.field_div [ ]
                    [ Control.control_div [ if model.IsWaitingReply then yield Control.isLoading ]
                        [ Textarea.textarea [ yield Textarea.props [
                                                DefaultValue model.Reply
                                                Ref (fun element ->
                                                    if not (isNull element) && model.Reply = "" then
                                                        let textarea = element :?> Browser.HTMLTextAreaElement
                                                        textarea.value <- model.Reply
                                                )
                                                OnChange (fun ev -> !!ev.target?value |> ChangeReply |> dispatch)
                                                OnKeyDown (fun ev ->
                                                    if ev.ctrlKey && ev.key = "Enter" then
                                                        dispatch Submit
                                                )
                                                ]
                                              if model.IsWaitingReply then yield Textarea.isDisabled ]
                        [ ] ]
                      Help.help [ Help.isDanger ]
                                [ str model.Error ] ]
                  Level.level [ ]
                    [ Level.left [ ]
                        [ Level.item [ ]
                            [ Button.button_a [ yield Button.isPrimary
                                                yield Button.onClick (fun _ -> dispatch Submit)
                                                if model.IsWaitingReply then yield Button.isDisabled ]
                                            [ str "Submit" ] ] ]
                      Level.item [ Level.Item.hasTextCentered ]
                        [ Help.help [ ]
                            [ str "You can use markdown to format your answer" ] ]
                      Level.right [ ]
                        [ Level.item [ Level.Item.props [ Key "test" ] ]
                            [ str "Press Ctrl + Enter to submit" ] ] ] ] ]

    let viewAnswers answers dispatch =
        div [ ]
            ( answers
                |> List.mapi (fun index answer -> Answer.view answer ((fun msg -> AnswerMsg (index, msg)) >> dispatch)))

    let view currentUser model dispatch =
        Section.section [ ]
            [ Heading.p [ Heading.is5 ]
                [ str model.Question.Title ]
              Columns.columns [ Columns.isCentered ]
                [ Column.column [ Column.Width.isTwoThirds ]
                    [ Views.Question.viewThread model.Question (viewAnswers model.Answers dispatch)
                      replyView currentUser model dispatch ] ] ]