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
            ]
            Initialize = None
            Interact = None
            Execute = fun (input, output) ->
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
