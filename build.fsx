#r "paket: groupref netcorebuild //"
#load ".fake/build.fsx/intellisense.fsx"
#if !FAKE
#r "Facades/netstandard"
#r "netstandard"
#endif

#nowarn "52"

open System
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators
open Fake.Tools.Git
open Fake.JavaScript

Target.create "Clean" (fun _ ->
    !! "src/bin"
    ++ "src/obj"
    ++ "output"
    |> Seq.iter Shell.cleanDir
)

Target.create "Install" (fun _ ->
    DotNet.restore
        (DotNet.Options.withWorkingDirectory __SOURCE_DIRECTORY__)
        "fulma-demo.sln"
)

Target.create "YarnInstall" (fun _ ->
    Yarn.install id
)

Target.create "Build" (fun _ ->
    let result =
        DotNet.exec
            (DotNet.Options.withWorkingDirectory __SOURCE_DIRECTORY__)
            "fable"
            "webpack-cli"

    if not result.OK then failwithf "dotnet fable failed with code %i" result.ExitCode
)

Target.create "Watch" (fun _ ->
    let result =
        DotNet.exec
            (DotNet.Options.withWorkingDirectory __SOURCE_DIRECTORY__)
            "fable"
            "webpack-dev-server"

    if not result.OK then failwithf "dotnet fable failed with code %i" result.ExitCode
)

// Where to push generated documentation
let githubLink = "git@github.com:MangelMaxime/fulma-demo.git"
let publishBranch = "gh-pages"
let fableRoot   = __SOURCE_DIRECTORY__
let temp        = fableRoot </> "temp"
let docsOuput = fableRoot </> "output"

// --------------------------------------------------------------------------------------
// Release Scripts
Target.create "PublishDocs" (fun _ ->
    Shell.cleanDir temp
    Repository.cloneSingleBranch "" githubLink publishBranch temp

    Shell.copyRecursive docsOuput temp true |> Trace.logfn "%A"
    Staging.stageAll temp
    Commit.exec temp (sprintf "Update site (%s)" (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")))
    Branches.push temp
)

// Build order
"Clean"
    ==> "Install"
    ==> "YarnInstall"
    ==> "Build"

"Watch"
    <== [ "YarnInstall" ]

"PublishDocs"
    <== [ "Build" ]

// start build
Target.runOrDefault "Build"
