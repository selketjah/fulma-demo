module Mailbox.Inbox

open Fulma
open Fulma.Extensions.Wikiki
open Fable.React
open Fable.React.Props
open Fable.FontAwesome
open Elmish
open System
open Helpers
open Types
open Fable.Core.JsInterop

type Model =
    {
        Emails : Map<Guid, Inbox.EmailMeta.Model>
        IsLoading : bool
        EmailView : Inbox.EmailView.Model option
        NumberOfChecked : int
    }

[<RequireQualifiedAccess>]
type FetchEmailListResult =
    | Success of emails : Email list
    | Errored of exn

[<RequireQualifiedAccess>]
type Read_Unread_Result =
    | Success of emails : Email list
    | Errored of exn

type Msg =
    | FetchEmailListResult of FetchEmailListResult
    | Open of Email
    | ToggleSelectAll
    | DeselectAll
    | EmailViewMsg of Inbox.EmailView.Msg
    | EmailMetaMsg of Guid * Inbox.EmailMeta.Msg
    | MarkAsRead
    | Read_Unread_Result of Read_Unread_Result
    | MarkAsUnRead
    | MoveToTrash
    | MoveToArchive
    | MoveToSpam

type Context =
    {
        PageRank : int
        Category : Email.Category
    }

let private fetchInboxEmails (context : Context) =
    promise {
        let! emails =
            API.Email.fetchInboxEmails context.PageRank context.Category

        return FetchEmailListResult.Success emails
    }

let init (context : Context) =
    {
        Emails = Map.empty
        IsLoading = true
        EmailView = None
        NumberOfChecked = 0
    }
    , Cmd.OfPromise.either fetchInboxEmails context FetchEmailListResult (FetchEmailListResult.Errored >> FetchEmailListResult)

// let updateEmailsFromList (updatedEmails : Email list) (model : Model) =
//     let emailToUpdate =
//         updatedEmails
//         |> List.map (fun email ->
//             email.Guid, email
//         )
//         |> Map.ofList

//     model.Emails
//     |> Map.map (fun key email ->

//     )


//     let rec updater (toUpdate : Email list) (state : Map<Guid, Inbox.EmailMeta.Model>) =
//         match toUpdate with
//         | email::tail ->
//             Map.add email.Guid (Inbox.EmailMeta.updateEmailValue email) state
//             |> updater tail
//         | [ ] ->
//             state

//     { model with
//         Emails = updater updatedEmails model.Emails
//     }

let update (msg : Msg) (model : Model) =
    match msg with
    | FetchEmailListResult result ->
        match result with
        | FetchEmailListResult.Success emails ->
            let emails =
                emails
                |> List.map (fun email ->
                    email.Guid, Inbox.EmailMeta.init email
                )
                |> Map.ofList

            { model with
                Emails = emails
                IsLoading = false
            }
            , Cmd.none

        | FetchEmailListResult.Errored error ->
            Logger.errorfn "Failed to retrieved the email list.\n%s" error.Message
            { model with
                IsLoading = false
            }
            , Cmd.none

    | Open email ->
        let (emailViewModel, emailViewCmd) = Inbox.EmailView.init email

        { model with
            EmailView = Some emailViewModel
        }
        , Cmd.map EmailViewMsg emailViewCmd

    | EmailViewMsg emailViewMsg ->
        match model.EmailView with
        | Some emailViewModel ->
            let (emailViewModel, emailViewCmd) = Inbox.EmailView.update emailViewMsg emailViewModel
            { model with
                EmailView = Some emailViewModel
            }
            , Cmd.map EmailViewMsg emailViewCmd

        | None ->
            model, Cmd.none

    | EmailMetaMsg (refGuid, emailMetaMsg) ->
        match Map.tryFind refGuid model.Emails with
        | Some emailMetaModel ->
            let (emailMetaModel, emailMetaCmd, externalMsg) = Inbox.EmailMeta.update emailMetaMsg emailMetaModel
            let model =
                { model with
                    Emails =
                        Map.add refGuid emailMetaModel model.Emails
                }

            let (model, extraCmd) =
                match externalMsg with
                | Inbox.EmailMeta.ExternalMsg.NoOp ->
                    model
                    , Cmd.none

                | Inbox.EmailMeta.ExternalMsg.Selected selectedEmail ->
                    { model with
                        // When an email become selected, it's unselect all the others
                        NumberOfChecked = 0
                    }
                    , Cmd.ofMsg (Open selectedEmail)

                | Inbox.EmailMeta.ExternalMsg.Checked selectedEmail ->
                    let cmd =
                        match model.EmailView with
                        | Some emailView ->
                            if emailView.Email.Guid = selectedEmail.Guid then
                                Cmd.none
                            else
                                Cmd.ofMsg (Open selectedEmail)
                        | None ->
                            Cmd.ofMsg (Open selectedEmail)

                    { model with
                        NumberOfChecked = model.NumberOfChecked + 1
                    }
                    , cmd

                | Inbox.EmailMeta.ExternalMsg.UnSelect _ ->
                    let numberOfChecked = model.NumberOfChecked - 1

                    if numberOfChecked = 0 then
                        { model with
                            EmailView = None
                            NumberOfChecked = numberOfChecked
                        }
                        , Cmd.none
                    else if numberOfChecked = 1 then
                        let email =
                            model.Emails
                            |> Map.filter (fun key value ->
                                value.IsChecked
                            )
                            |> Seq.head
                        { model with
                            EmailView = None
                            NumberOfChecked = numberOfChecked
                        }
                        , Cmd.ofMsg (Open email.Value.Email)
                    else
                        { model with
                            NumberOfChecked = numberOfChecked
                        }
                        , Cmd.none

            model
            , Cmd.batch
                [
                    Cmd.map EmailMetaMsg emailMetaCmd
                    extraCmd
                ]

        | None ->
            model, Cmd.none

    | ToggleSelectAll ->
        let selectAll = model.NumberOfChecked < model.Emails.Count
        printfn "selectAll: %b" selectAll
        { model with
            NumberOfChecked =
                if selectAll then
                    model.Emails.Count
                else
                    0
            Emails =
                model.Emails
                |> Map.map (fun key value ->
                    if selectAll then
                        Inbox.EmailMeta.select value
                    else
                        Inbox.EmailMeta.unselect value
                )
            EmailView = None
        }
        , Cmd.none

    | DeselectAll ->
        { model with
            NumberOfChecked = 0
            Emails =
                model.Emails
                |> Map.map (fun key value ->
                    Inbox.EmailMeta.unselect value
                )
            EmailView = None
        }
        , Cmd.none

    | MarkAsRead ->
        if model.NumberOfChecked = 0 then
            model
            , Cmd.none
        else
            let guids =
                model.Emails
                |> Seq.filter (fun kv ->
                    kv.Value.IsChecked
                )
                |> Seq.map (fun kv ->
                    kv.Key
                )
                |> Seq.toList

            model
            , Cmd.OfPromise.either
                API.Email.markAsRead
                guids
                (Read_Unread_Result.Success >> Read_Unread_Result)
                (Read_Unread_Result.Errored >> Read_Unread_Result)

    | MarkAsUnRead ->
        // If there is no checked email
        if model.NumberOfChecked = 0 then
            // Try to see if we have an opened email
            match model.EmailView with
            | Some emailView ->
                model
                , Cmd.OfPromise.either
                    API.Email.markAsUnread
                    [ emailView.Email.Guid ]
                    (Read_Unread_Result.Success >> Read_Unread_Result)
                    (Read_Unread_Result.Errored >> Read_Unread_Result)
            | None ->
                model
                , Cmd.none

        else
            let guids =
                model.Emails
                |> Seq.filter (fun kv ->
                    kv.Value.IsChecked
                )
                |> Seq.map (fun kv ->
                    kv.Key
                )
                |> Seq.toList

            model
            , Cmd.OfPromise.either
                API.Email.markAsUnread
                guids
                (Read_Unread_Result.Success >> Read_Unread_Result)
                (Read_Unread_Result.Errored >> Read_Unread_Result)

    | Read_Unread_Result result ->
        match result with
        | Read_Unread_Result.Success updatedEmails ->
            let cmds =
                updatedEmails
                |> List.map (fun updatedEmail ->
                    Cmd.ofMsg (EmailMetaMsg (updatedEmail.Guid, Inbox.EmailMeta.UpdateEmail updatedEmail))
                )
                |> Cmd.batch

            { model with
                EmailView = None
            }
            , cmds

        | Read_Unread_Result.Errored error ->
            Logger.errorfn "An error occured.\n%s" error.Message
            model
            , Cmd.none

    | MoveToTrash ->
        model
        , Cmd.none

    | MoveToArchive ->
        model
        , Cmd.none

    | MoveToSpam ->
        model
        , Cmd.none


let buttonIcon icon onClickMsg dispatch =
    Button.button
        [
            Button.OnClick (fun _ ->
                dispatch onClickMsg
            )
        ]
        [
            Icon.icon [ ]
                [
                    Fa.i [ icon ]
                        [ ]
                ]
        ]

let menubar (model : Model) (dispatch : Dispatch<Msg>) =
    Level.level
        [
            Level.Level.CustomClass "is-menubar"
            Level.Level.Modifiers [ Modifier.IsMarginless ]
        ]
        [
            Level.left [ ]
                [
                    Field.div
                        [
                            Field.Props
                                [
                                    Style
                                        [
                                            MarginBottom "0"
                                            MarginLeft "1rem"
                                        ]
                                ]
                        ]
                        [
                            Checkradio.checkboxInline
                                [
                                    Checkradio.Id "inbox-select-all"
                                    Checkradio.Color IsPrimary
                                    Checkradio.CustomClass "is-outlined"
                                    Checkradio.OnChange (fun _ ->
                                        dispatch ToggleSelectAll
                                    )
                                    Checkradio.Checked (model.NumberOfChecked = model.Emails.Count)
                                ]
                                [ ]
                        ]

                    Button.list [ Button.List.HasAddons ]
                        [
                            buttonIcon Fa.Solid.Eye MarkAsRead dispatch
                            buttonIcon Fa.Solid.EyeSlash MarkAsUnRead dispatch
                        ]
                    Button.list [ Button.List.HasAddons ]
                        [
                            buttonIcon Fa.Regular.TrashAlt MoveToTrash dispatch
                            buttonIcon Fa.Solid.Archive MoveToArchive dispatch
                            buttonIcon Fa.Solid.Ban MoveToSpam dispatch
                        ]
                ]
            Level.right [ ]
                [
                    Button.list [ Button.List.HasAddons ]
                        [
                            buttonIcon Fa.Solid.AngleLeft (unbox "todo") dispatch
                            buttonIcon Fa.Solid.AngleDown (unbox "todo") dispatch
                            buttonIcon Fa.Solid.AngleRight (unbox "todo") dispatch
                        ]
                ]
        ]

let private renderActiveEmail (email : Email) =
    div [ Class "email-view" ]
        [
            Level.level [ Level.Level.CustomClass "is-header" ]
                [
                    Level.left [ ]
                        [
                            Level.item [ ]
                                [ Heading.h4 [ ]
                                    [ str email.Subject ]
                                ]
                        ]
                    Level.right [ ]
                        [ Icon.icon [ ]
                            [
                                Fa.i
                                    [
                                        Fa.Regular.Star
                                        Fa.Size Fa.FaLarge
                                    ]
                                    [ ]
                            ]
                        ]
                ]
        ]

let private summaryItemWithIcon (text : string) (icon : Fa.IconOption) (onClickMsg : Msg) (dispatch : Dispatch<Msg>) =
    div [
            Class "checked-summary-item"
            OnClick (fun _ ->
                dispatch onClickMsg
            )
        ]
        [
            a
                [

                ]
                [
                    Icon.icon
                        [
                            Icon.Size IsSmall
                            Icon.CustomClass "has-text"
                        ]
                        [
                            Fa.i
                                [
                                    icon
                                ]
                                [ ]
                        ]
                    span [ ]
                        [ str text ]
                ]
        ]

let private renderCheckedEmailsView (model : Model) (dispatch : Dispatch<Msg>) =
    let text =
        sprintf "%i conversations selected" model.NumberOfChecked

    let actionButton =
        if model.NumberOfChecked > 1 then
            Button.button
                [
                    Button.Color IsPrimary
                    Button.OnClick (fun _ ->
                        dispatch DeselectAll
                    )
                ]
                [ str "Deselect all" ]
        else
            nothing

    div [ Class "checked-summary" ]
        [
            div [ Class "checked-summary-item is-centered" ]
                [
                    Icon.icon [ Icon.Size IsMedium ]
                        [
                            Fa.i
                                [
                                     Fa.Solid.CheckSquare
                                     Fa.Size Fa.Fa2x
                                ]
                                [ ]
                        ]
                ]

            div [ Class "checked-summary-item is-centered" ]
                [
                    Text.div
                        [
                            Modifiers
                                [
                                    Modifier.TextSize (Screen.All, TextSize.Is5)
                                    Modifier.TextColor IsGrey
                                ]
                        ]
                        [
                            str text
                        ]

                ]

            div [ ]
                [
                    summaryItemWithIcon "Delete" Fa.Solid.TrashAlt MoveToTrash dispatch
                    summaryItemWithIcon "Mark as read" Fa.Solid.EnvelopeOpen MarkAsRead dispatch
                    summaryItemWithIcon "Mark as unread" Fa.Solid.Envelope MarkAsUnRead dispatch
                    hr [ ]
                    summaryItemWithIcon "Cancel" Fa.Solid.Times DeselectAll dispatch
                ]
        ]

let private renderNoSelectionSummary =
    div [ Class "checked-summary" ]
        [
            div [ Class "checked-summary-item is-centered" ]
                [
                    Icon.icon
                        [
                            Icon.Size IsLarge
                        ]
                        [
                            Fa.i
                                [
                                    Fa.Solid.EnvelopeOpenText
                                    Fa.Size Fa.Fa4x
                                ]
                                [ ]
                        ]
                ]

            div [ Class "checked-summary-item is-centered" ]
                [
                    Text.div
                        [
                            Modifiers
                                [
                                    Modifier.TextSize (Screen.All, TextSize.Is5)
                                    Modifier.TextColor IsGrey
                                ]
                        ]
                        [ str "Select an element to read it" ]
                ]
        ]

let view (model : Model) (dispatch : Dispatch<Msg>) =
    let emailMetaDispatch (guid : Guid) =
        Curry.apply EmailMetaMsg guid >> dispatch

    let emailList =
        if model.IsLoading then
            div [ Class "loader-dual-ring-container" ]
                [
                    div [ Class "loader-dual-ring is-medium is-black" ]
                        [ ]
                ]
        else
            model.Emails
            |> Seq.sortByDescending (fun keyValue ->
                keyValue.Value.SortableKey
            )
            |> Seq.map (fun keyValue ->
                Inbox.EmailMeta.view
                    {
                        Model = keyValue.Value
                        // IsChecked = Set.contains keyValue.Key model.CheckedEmails
                        // OnCheck = ToggleCheck >> dispatch
                        Dispatch = (emailMetaDispatch keyValue.Key)
                    }

            )
            |> Seq.toList
            |> ofList

    let rightColumnContent =
        match model.EmailView with
        | Some emailViewModel ->
            // Only render the emailView if we have no selection
            if model.NumberOfChecked > 1 then
                renderCheckedEmailsView model dispatch
            else
                Inbox.EmailView.view emailViewModel (EmailViewMsg >> dispatch)

        | None ->
            if model.NumberOfChecked > 1 then
                renderCheckedEmailsView model dispatch
            else
                renderNoSelectionSummary




    div [ Class "inbox-container" ]
        [
            menubar model dispatch
            Columns.columns [ Columns.IsGapless ]
                [
                    Column.column
                        [
                            Column.CustomClass "is-email-list"
                            Column.Width (Screen.All, Column.Is5)
                        ]
                        [ emailList ]

                    Column.column [ Column.Width (Screen.All, Column.Is7) ]
                        [
                            rightColumnContent
                        ]
                ]
        ]