module Mailbox

open Fulma
open Elmish
open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open Types


type Settings =
    | Category of Email.Category


type Model =
    {
        ActiveCategory : Email.Category
        Inbox : Mailbox.Inbox.Model
    }


type Msg =
    | InboxMsg of Mailbox.Inbox.Msg


let private initInbox (pageRank : int option) (category : Email.Category) =
    let (inboxModel, inboxCmd) =
        Mailbox.Inbox.init
            {
                PageRank =
                    Option.defaultValue 1 pageRank
                Category = category
            }

    {
        ActiveCategory = category
        Inbox = inboxModel
    }
    , Cmd.map InboxMsg inboxCmd


let init (route : Router.MailboxRoute) =
    match route with
    | Router.MailboxRoute.Inbox pageRank ->
        initInbox pageRank Email.Category.Inbox

    | Router.MailboxRoute.Archive pageRank ->
        initInbox pageRank Email.Category.Archive

    | Router.MailboxRoute.Sent pageRank ->
        initInbox pageRank Email.Category.Sent

    | Router.MailboxRoute.Stared pageRank ->
        initInbox pageRank Email.Category.Stared

    | Router.MailboxRoute.Trash pageRank ->
        initInbox pageRank Email.Category.Trash

let update (msg  : Msg) (model : Model) =
    match msg with
    | InboxMsg inboxMsg ->
        let (inboxModel, inboxCmd) = Mailbox.Inbox.update inboxMsg model.Inbox
        { model with
            Inbox = inboxModel
        }
        , Cmd.map InboxMsg inboxCmd


let private standardCategoryItem
    (props :
        {|
            IsActive : bool
            Route : Router.Route
            Icon : Fa.IconOption
            Text : string
        |}
    ) =
    Menu.Item.li
        [
            Menu.Item.IsActive props.IsActive
            Menu.Item.OnClick (fun _ ->
                Router.modifyLocation props.Route
            )
        ]
        [
            Icon.icon [ ]
                [
                    Fa.i [ props.Icon ]
                        [ ]
                ]
            str props.Text
        ]

let private renderFolderItem (txt : string) (color : string) =
    Menu.Item.li [ ]
        [
            Icon.icon [ Icon.Props [ Style [ Color color ] ] ]
                [
                    Fa.i [ Fa.Solid.Folder ]
                        [ ]
                ]
            str txt
        ]

let private renderTagItem (txt : string) (color : string) =
    Menu.Item.li [ ]
        [
            Icon.icon [ Icon.Props [ Style [ Color color ] ] ]
                [
                    Fa.i [ Fa.Solid.Tag ]
                        [ ]
                ]
            str txt
        ]

let private sideMenu (model : Model) (dispatch : Dispatch<Msg>) =
    Menu.menu [ CustomClass "sidebar-main" ]
        [
            Menu.list [ ]
                [
                    standardCategoryItem
                        {|
                            IsActive =
                                model.ActiveCategory = Email.Category.Inbox
                            Route =
                                Router.MailboxRoute.Inbox None
                                |> Router.Mailbox
                            Icon = Fa.Solid.Inbox
                            Text = "Inbox"
                        |}
                    standardCategoryItem
                        {|
                            IsActive =
                                model.ActiveCategory = Email.Category.Sent
                            Route =
                                Router.MailboxRoute.Sent None
                                |> Router.Mailbox
                            Icon = Fa.Solid.Envelope
                            Text = "Sent"
                        |}
                    standardCategoryItem
                        {|
                            IsActive =
                                model.ActiveCategory = Email.Category.Archive
                            Route =
                                Router.MailboxRoute.Archive None
                                |> Router.Mailbox
                            Icon = Fa.Solid.Archive
                            Text = "Archive"
                        |}
                    standardCategoryItem
                        {|
                            IsActive =
                                model.ActiveCategory = Email.Category.Stared
                            Route =
                                Router.MailboxRoute.Stared None
                                |> Router.Mailbox
                            Icon = Fa.Solid.Star
                            Text = "Stared"
                        |}
                    standardCategoryItem
                        {|
                            IsActive =
                                model.ActiveCategory = Email.Category.Trash
                            Route =
                                Router.MailboxRoute.Trash None
                                |> Router.Mailbox
                            Icon = Fa.Solid.TrashAlt
                            Text = "Trash"
                        |}
                ]

            Menu.label [ ]
                [
                    str "Folders"
                ]
            Menu.list [ ]
                [
                    renderFolderItem "Bills" "#e6984c"
                    renderFolderItem "OSS" "#c793ca"
                ]

            Menu.label [ ]
                [
                    str "Tags"
                ]
            Menu.list [ ]
                [
                    renderTagItem "Github" "#c793ca"
                    renderTagItem "Gitlab" "#c793ca"
                ]
        ]


let view (model : Model) (dispatch : Dispatch<Msg>) =
    Columns.columns
        [
            Columns.CustomClass "is-inbox"
            Columns.IsGapless
        ]
        [
            Column.column
                [
                    Column.CustomClass "is-main-menu"
                    Column.Width (Screen.All, Column.Is2)
                ]
                [
                    Text.div
                        [
                            Modifiers
                                [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ]
                            Props
                                [ Style
                                    [ Padding "2rem 2rem 1rem" ]
                                ]
                        ]
                        [
                            Button.button
                                [
                                    Button.Color IsPrimary
                                    Button.IsFullWidth
                                    Button.Modifiers [ Modifier.TextWeight TextWeight.Bold ]
                                ]
                                [ str "Compose" ]
                        ]
                    sideMenu model dispatch
                ]
            Column.column [ ]
                [
                    Mailbox.Inbox.view model.Inbox (InboxMsg >> dispatch)
                ]
        ]