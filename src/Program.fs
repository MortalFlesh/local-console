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

        command "repository:create" {
            Description = "Create backuped repositories - command will download all repositories from backup file."
            Help = commandHelp [
                "Limitation of current implementation is that it cant clone repository from ssh (it could be solved by ...)."
                "Yet for now, just use <c:yellow>--as-shell</c> option to create a shell script, which will have your credentials when you run it."
            ]
            Arguments = [
                Argument.required "backup" "Path to directory with backup files."
            ]
            Options = [
                Option.optionalArray "ignore-remote" None "Remote url to ignore" None
                Option.noValue "dry-run" None "Whether to run command just to output what it would have done."
                Option.noValue "as-shell" None "Whether to just create a shell script, which will do the work."
            ]
            Initialize = None
            Interact = None
            Execute = fun (input, output) ->
                let backupDir = input |> Input.getArgumentValue "backup"

                let ignoreRemotes =
                    match input with
                    | Input.OptionListValue "ignore-remote" ignored -> ignored
                    | _ -> []

                backupDir
                |> RepositoryCreateCommand.execute output ignoreRemotes (
                    match input with
                    | Input.HasOption "dry-run" _ -> RepositoryCreateCommand.Mode.DryRun
                    | Input.HasOption "as-shell" _ -> RepositoryCreateCommand.Mode.CreateShell
                    | _ -> RepositoryCreateCommand.Mode.CreateRepositories
                )

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
