module Helpers

module Random =

    open Fable.Core

    let between (min : float) (max : float) =
        JS.Math.floor(JS.Math.random() * (max - min + 1.) + 1.)

module Set =

    let toggle (item : 'T) (set : Set<'T>) =
        if Set.contains item set then
            Set.remove item set
        else
            Set.add item set

type Classes =

    static member fromList (classes : (string * bool) list) =
        classes
        |> List.filter snd
        |> List.map fst
        |> String.concat " "

    static member fromListWithBase (baseClass : string) (classes : (string * bool) list) =
        Classes.fromList classes
        |> (+) (baseClass + " ")

module HMR =

    let hot = HMR.``module``.hot

    /// Normal structural F# comparison, but ignores top-level functions (e.g. Elmish dispatch).
    /// Can be used e.g. with the `FunctionComponent.Of` `memoizeWith` parameter.
    let equalsButFunctions (x: 'a) (y: 'a) =
        #if DEBUG
            if hot.status() = HMR.Status.Apply then
                false
            else
                Fable.React.Helpers.equalsButFunctions x y
        #else
            Fable.React.Helpers.equalsButFunctions
        #endif

module Cmd =

    open Elmish
    open System

    let mapWithGuid
            (outMsg : Guid * 'subCmd -> 'msg)
            (guid : Guid)
            (cmd : Cmd<'subCmd>) : Cmd<'msg> =
        Cmd.map (fun msg -> outMsg (guid, msg)) cmd

    module OfFunc =

        let execute (task: 'a -> _) (arg: 'a) : Cmd<'msg> =
            let bind dispatch =
                try
                    task arg
                with _ ->
                    ()
            [bind]

module Curry =
    let apply func a b = func (a,b)

    let apply2 func a b c = func (a, b, c)

    let apply3 func a b c d = func (a, b, c, d)

    let apply4 func a b c d e = func (a, b, c, d, e)

    let apply5 func a b c d e f = func (a, b, c, d, e, f)

    let apply6 func a b c d e f g = func (a, b, c, d, e, f, g)


module Uncurry =
    let apply func (a,b) = func a b

    let apply2 func (a, b, c) = func a b c

    let apply3 func (a, b, c, d) = func a b c d

    let apply4 func (a, b, c, d, e) = func a b c d e

    let apply5 func (a, b, c, d, e, f) = func a b c d e f

    let apply6 func (a, b, c, d, e, f, g) = func a b c d e f g