// ========================================================================================================
// === F# / Project fake build ==================================================================== 1.0.0 =
// --------------------------------------------------------------------------------------------------------
// Options:
//  - no-clean   - disables clean of dirs in the first step (required on CI)
//  - no-lint    - lint will be executed, but the result is not validated
// ========================================================================================================

open Fake.Core
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators

open ProjectBuild
open Utils

[<EntryPoint>]
let main args =
    args |> Args.init

    Targets.init {
        Project = {
            Name = "Local Console"
            Summary = "Console application for local helpers."
            Git = Git.init ()
        }
        Specs = Spec.defaultConsoleApplication [
            OSX
            // Windows
        ]
    }

    args |> Args.run
