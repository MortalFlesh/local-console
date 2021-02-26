open System
open System.IO
open MF.ConsoleApplication
open MF.LocalConsole
open MF.LocalConsole.Console
open FSharp.Data
open System.Collections.Concurrent

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

        command "repository:restore" {
            Description = "Restore backuped repositories - command will restore all repositories out of a backup, created by repository:backup command."
            Help = commandHelp [
                "The <c:dark-green>{{command.name}}</c> restores repositories:"
                "        <c:dark-green>dotnet {{command.full_name}}</c> <c:dark-yellow>path-to-repositories/</c>"
            ]
            Arguments = [
                Argument.required "backup" "Path to dir containing backup."
            ]
            Options = [
                Option.optional "ignore-remote" None "Path to file where there are remotes which will be ignored." None
                Option.noValue "dry-run" None "Whether to just show a result as stdout."
                Option.noValue "use-shell" None "Whether to create a shell script to create repositories."
            ]
            Initialize = None
            Interact = None
            Execute = fun (input, output) ->
                let backupDir = input |> Input.getArgumentValue "backup"

                let mode =
                    match input with
                    | Input.HasOption "dry-run" _ -> RepositoryCreateCommand.DryRun
                    | Input.HasOption "use-shell" _ -> RepositoryCreateCommand.CreateShell
                    | _ -> RepositoryCreateCommand.CreateRepositories

                let ignoredRemotes =
                    match input with
                    | Input.OptionValue "ignore-remote" ignored -> ignored |> FileSystem.readLines
                    | _ -> []

                backupDir
                |> RepositoryCreateCommand.execute output ignoredRemotes mode

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

        command "azure:func" {
            Description = "Calls a azure function."
            Help = commandHelp [
                "The <c:dark-green>{{command.name}}</c> calls azure function 10 times:"
                "        <c:dark-green>dotnet {{command.full_name}}</c> <c:dark-yellow>app-name</c> <c:dark-yellow>function-name</c> <c:dark-magenta>10</c> --code=<your-api-key>"
            ]
            Arguments = [
                Argument.required "app-name" "Name of the azure application."
                Argument.required "function-name" "Name of the function."
                Argument.required "call-count" "Count of calls to be made."
            ]
            Options = [
                Option.required "code" (Some "c") "Code to your function." None
            ]
            Initialize = None
            Interact = None
            Execute = fun (input, output) ->
                let appName = input |> Input.getArgumentValueAsString "app-name" |> Option.defaultValue "-"
                let functionName = input |> Input.getArgumentValueAsString "function-name" |> Option.defaultValue "-"
                let callCount = input |> Input.getArgumentValueAsString "call-count" |> Option.defaultValue "0" |> int
                let code = input |> Input.getOptionValueAsString "code" |> Option.defaultValue "-"

                output.Table ["app"; "func"; "calls"; "code"] [
                    [ appName; functionName; string callCount; code ]
                ]

                output.SubTitle "Starting ..."
                let progress = callCount |> output.ProgressStart "Function calls"

                let baseUrl = sprintf "https://%s.azurewebsites.net/api/%s?code=%s" appName functionName code
                let requests = ConcurrentDictionary<int, string>()

                [
                    for i in 0..callCount do
                        yield async {
                            let! rawResponse =
                                Http.AsyncRequestString
                                    (
                                        baseUrl,
                                        httpMethod = "POST",
                                        headers = [
                                            HttpRequestHeaders.Accept "application/vnd.api+json"
                                            HttpRequestHeaders.ContentType "application/vnd.api+json"
                                        ],
                                        body = TextRequest
                                            """{
                                                "data": {
                                                    "type": "normalization",
                                                    "attributes": {
                                                        "contact": {
                                                            "email": " JaN.Novak@seznam.cz ",
                                                            "phone": "603 123 122"
                                                        }
                                                    }
                                                }
                                            }"""
                                    )
                                |> Async.Catch

                            progress |> output.ProgressAdvance

                            return
                                match rawResponse with
                                | Choice1Of2 response ->
                                    requests.AddOrUpdate(i, response, (fun _ _ -> response)) |> ignore

                                    [ string i; "<c:green>Ok</c>" ]
                                | Choice2Of2 e ->
                                    let response = sprintf "Error: %A" e.Message
                                    requests.AddOrUpdate(i, response, (fun _ _ -> response)) |> ignore

                                    [ string i; "<c:red>Error</c>" ]
                        }
                ]
                |> Async.Parallel
                |> Async.RunSynchronously
                |> tee (fun _ -> progress |> output.ProgressFinish; output.NewLine())
                |> Seq.toList
                |> output.Table [ "Request"; "Response" ]

                if output.IsDebug() then
                    output.Title "Real responses"

                    requests
                    |> Seq.toList
                    |> List.map (fun kv -> [kv.Key |> string; kv.Value])
                    |> output.Table [ "Request"; "Response" ]

                output.Success "Done"
                ExitCode.Success
        }

        command "normalize:file" {
            Description = "Call a normalize function for each line of the file."
            Help = None
            Arguments = [
                Argument.required "file-name" "Name of the file to normalize."
                Argument.required "input-type" "Type of the input data (<c:blue>email</c>, <c:blue>phone</c>)."
            ]
            Options = [
                Option.required "code" (Some "c") "Code to your function." None
                Option.optional "output" (Some "o") "Output dir." None
            ]
            Initialize = None
            Interact = None
            Execute = NormalizeCommand.execute
        }

        command "normalize:phone" {
            Description = "Normalize a single phone number"
            Help = None
            Arguments = [
                Argument.required "phone" "Phone number input."
            ]
            Options = []
            Initialize = None
            Interact = None
            Execute = fun (input, output) ->
                let phone = input |> Input.getArgumentValueAsString "phone" |> Option.defaultValue ""

                let tryParsePhone code phone =
                    let phoneNumberUtil = PhoneNumbers.PhoneNumberUtil.GetInstance()
                    phoneNumberUtil.Parse(phone, code)

                try
                    let parsedPhone = tryParsePhone null phone

                    output.Table [ "original"; "p.code"; "p.nationalNumber" ] [
                        [
                            phone
                            parsedPhone.CountryCode |> string
                            parsedPhone.NationalNumber |> string
                        ]
                    ]
                with
                | e ->
                    try
                        let parsedPhone = tryParsePhone "CZ" phone

                        output.Table [ "original"; "p.code"; "p.nationalNumber" ] [
                            [
                                phone
                                parsedPhone.CountryCode |> string
                                parsedPhone.NationalNumber |> string
                            ]
                        ]

                    with
                    | _ ->
                        e.Message
                        |> sprintf "Phone %A is not valid: %A" phone
                        |> output.Error

                ExitCode.Success
        }

        command "stats:phone:code" {
            Description = "Search phone codes in the file and show stats for them."
            Help = None
            Arguments = [
                Argument.required "file-name" "Name of the file to normalize."
            ]
            Options = [
                Option.optional "output" (Some "o") "Output dir." None
            ]
            Initialize = None
            Interact = None
            Execute = PhoneCodeStatsCommand.execute
        }

        command "stats:contacts" {
            Description = "Show stats for normalized files."
            Help = None
            Arguments = [
                Argument.required "file-name" "Name of the file to normalize."
            ]
            Options = []
            Initialize = None
            Interact = None
            Execute = ContactStatsCommand.execute
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
