namespace MF.LocalConsole

[<RequireQualifiedAccess>]
module RepositoryCreateCommand =
    open System.IO
    open FSharp.Data
    open MF.ConsoleApplication
    open LibGit2Sharp

    type BackupSchema = JsonProvider<"src/schema/backup.json">

    open Path.Operators

    let execute (output: MF.ConsoleApplication.Output) (ignoredRemotes: string list) backupDir =
        let repositories =
            [ backupDir ]
            |> FileSystem.getAllFiles
            |> List.filter (fun f -> f.EndsWith("backup.json"))

        let mutable reposWithError = []

        let rec copyFiles repository backupRepositoryDir targetDir = function
            | [] -> ()
            | files ->
                files
                |> List.iter (fun file ->
                    let fileTargetPath = targetDir / file

                    if Directory.Exists (backupRepositoryDir / file)
                        then
                            backupRepositoryDir / file
                            |> Directory.GetFiles
                            |> Seq.toList
                            |> copyFiles repository (backupRepositoryDir / file) fileTargetPath
                        else
                            let fileTargetDir = fileTargetPath |> Path.dirName

                            Directory.ensure fileTargetDir

                            try
                                File.Copy(backupRepositoryDir / file, fileTargetPath, true)
                            with
                            | e ->
                                let message = sprintf "! %s" e.Message

                                reposWithError <- [repository; message] :: reposWithError
                )

        repositories
        |> List.iter (fun backupFile ->
            let repositoryBackupPath = Path.GetDirectoryName(backupFile)
            let repository = backupFile |> File.ReadAllText |> BackupSchema.Parse

            output.Section <| sprintf "Create %s" repository.Name

            let topLevelDir = repository.Path |> Path.dirName
            Directory.ensure topLevelDir    // create top level directory (not repository dir)

            let remotes = repository.Remotes |> Seq.toList

            if remotes |> List.exists (fun r -> ignoredRemotes |> List.exists (fun ignored -> r.Url.Contains(ignored)))
                then output.Message "<c:yellow> - skipped for remote</c>"
                else
                    let url =
                        remotes
                        |> List.tryFind (fun r -> r.Name = "origin")
                        |> Option.bind (fun _ -> repository.Remotes |> Seq.tryHead)
                        |> Option.map (fun r -> r.Url)

                    match url with
                    | Some url ->
                        Repository.Clone(url, topLevelDir) |> ignore
                    | _ ->
                        output.Message "<c:yellow> - no remote defined</c>"
                        Directory.ensure repository.Path

                    repository.Ignored |> Seq.toList |> copyFiles repository.Name repositoryBackupPath repository.Path
                    repository.Untracked |> Seq.toList |> copyFiles repository.Name repositoryBackupPath repository.Path
                    repository.NotCommited |> Seq.toList |> List.distinct |> copyFiles repository.Name repositoryBackupPath repository.Path

                    output.Success " - done"
        )

        reposWithError
            |> List.distinct
            |> List.sort
            |> output.Options "Repository with errors:"

        ()
