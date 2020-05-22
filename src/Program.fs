open System
open MF.ConsoleApplication
open MF.LocalConsole
open MF.LocalConsole.Console

[<EntryPoint>]
let main argv =
    consoleApplication {
        title AssemblyVersionInformation.AssemblyProduct
        info ApplicationInfo.MainTitle
        version AssemblyVersionInformation.AssemblyVersion

        command "repository:backup" {
            Description = "Backup repositories - command will save all repository remote urls to the output file."
            Help = commandHelp [
                    "The <c:dark-green>{{command.name}}</c> saves repository remote urls:"
                    "        <c:dark-green>dotnet {{command.full_name}}</c> <c:dark-yellow>path-to-repositories/</c>"
                ]
            Arguments = [
                Argument.repositories
            ]
            Options = [
                Option.outputFile
                Option.optional "ignore-repo" None "Path to file where there are repositories which will be ignored no matter where they are." None
                Option.optional "ignore-file" None "Path to file where there are files/paths which will be ignored from currently ignored files in repositories." None
                Option.noValue "only-complete" None "Select only repositories with all changes commited."
                Option.noValue "only-incomplete" None "Select only repositories where are some uncommited changes (untracked files, modified, ...)."
            ]
            Initialize = None
            Interact = None
            Execute = fun (input, output) ->
                let paths = input |> Input.getRepositories
                let outputFile =
                    match input with
                    | Input.OptionOptionalValue "output" file -> Some file
                    | _ -> None

                let completeRepository =
                    match input with
                    | Input.HasOption "only-complete" _ -> RepositoryBackupCommand.CompleteRepository.OnlyComlete
                    | Input.HasOption "only-incomplete" _ -> RepositoryBackupCommand.CompleteRepository.OnlyIncomplete
                    | _ -> RepositoryBackupCommand.CompleteRepository.All

                let ignoredFiles =
                    match input with
                    | Input.OptionValue "ignore-file" ignored -> ignored |> FileSystem.readLines
                    | _ -> []

                let ignoredRepositories =
                    match input with
                    | Input.OptionValue "ignore-repo" ignored -> ignored |> FileSystem.readLines
                    | _ -> []

                paths
                |> RepositoryBackupCommand.execute output completeRepository ignoredFiles ignoredRepositories (
                    match outputFile with
                    | Some file -> RepositoryBackupCommand.Output.File file
                    | _ -> RepositoryBackupCommand.Output.Stdout output
                )

                output.Success "Done"
                ExitCode.Success
        }

        command "repository:build:list" {
            Description = "List all repositories for the build.fsx version and type."
            Help = commandHelp [
                    "The <c:dark-green>{{command.name}}</c> saves repository remote urls:"
                    "        <c:dark-green>dotnet {{command.full_name}}</c> <c:dark-yellow>path-to-repositories/</c>"
                ]
            Arguments = [
                Argument.repositories
            ]
            Options = [
                Option.required "type" (Some "t") "Show only builds of this type." None
                Option.required "build-version" (Some "b") "Show only builds of this version." None
            ]
            Initialize = None
            Interact = None
            Execute = fun (input, output) ->
                let paths = input |> Input.getRepositories

                let filter: RepositoryBuildListCommand.Filter = {
                    BuildType =
                        match input with
                        | Input.OptionOptionalValue "type" buildType -> Some buildType
                        | _ -> None
                    Version =
                        match input with
                        | Input.OptionOptionalValue "build-version" version -> Some version
                        | _ -> None
                }

                paths
                |> RepositoryBuildListCommand.execute output filter

                output.Success "Done"
                ExitCode.Success
        }

        command "about" {
            Description = "Displays information about the current project."
            Help = None
            Arguments = []
            Options = []
            Initialize = None
            Interact = None
            Execute = fun (_input, output) ->
                let ``---`` = [ "------------------"; "----------------------------------------------------------------------------------------------" ]

                output.Table [ AssemblyVersionInformation.AssemblyProduct ] [
                    [ "Description"; AssemblyVersionInformation.AssemblyDescription ]
                    [ "Version"; AssemblyVersionInformation.AssemblyVersion ]

                    ``---``
                    [ "Environment" ]
                    ``---``
                    [ ".NET Core"; Environment.Version |> sprintf "%A" ]
                    [ "Command Line"; Environment.CommandLine ]
                    [ "Current Directory"; Environment.CurrentDirectory ]
                    [ "Machine Name"; Environment.MachineName ]
                    [ "OS Version"; Environment.OSVersion |> sprintf "%A" ]
                    [ "Processor Count"; Environment.ProcessorCount |> sprintf "%A" ]

                    ``---``
                    [ "Git" ]
                    ``---``
                    [ "Branch"; AssemblyVersionInformation.AssemblyMetadata_gitbranch ]
                    [ "Commit"; AssemblyVersionInformation.AssemblyMetadata_gitcommit ]
                ]

                ExitCode.Success
        }
    }
    |> run argv
