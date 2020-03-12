namespace MF.LocalConsole

[<RequireQualifiedAccess>]
module RepositoryBackupCommand =
    open System.IO
    open MF.ConsoleApplication
    open LibGit2Sharp

    type Output =
        | Stdout of MF.ConsoleApplication.Output
        | File of string

    type CompleteRepository =
        | All
        | OnlyComlete
        | OnlyIncomplete

    open Path.Operators

    let execute (output: MF.ConsoleApplication.Output) completeRepository ignoredFiles ignoredRepositories commandOutput paths =
        let entryPath (entry: StatusEntry) = entry.FilePath

        let repositories =
            paths
            |> FileSystem.getAllDirs
            |> List.filter (String.trimEnd '/' >> sprintf "%s/.git" >> Directory.Exists)

        let repositories =
            match ignoredRepositories with
            | [] -> repositories
            | ignoredRepositories ->
                repositories
                |> List.filterNotInBy Path.fileName ignoredRepositories

        let progress = repositories |> List.length |> output.ProgressStart "Getting repository informations ..."

        let repositories =
            repositories
            //|> List.take 5 // todo !!!
            |> List.map (fun path ->
                use repo = new Repository(path)

                let status = repo.RetrieveStatus()

                {
                    Path = path
                    Name = path |> Path.fileName
                    Remotes = repo.Network.Remotes |> Seq.map (fun r -> { Name = r.Name; Url = r.Url }) |> Seq.toList
                    Untracked = [
                        yield! status.Untracked |> Seq.map entryPath
                    ]
                    NotCommited = [
                        yield! status.Added |> Seq.map entryPath
                        yield! status.Modified |> Seq.map entryPath
                        yield! status.Removed |> Seq.map entryPath
                        yield! status.RenamedInIndex |> Seq.map entryPath
                        yield! status.Staged |> Seq.map entryPath
                        yield! status.Unaltered |> Seq.map entryPath
                    ]
                    Ignored =
                        [
                            yield! status.Ignored |> Seq.map entryPath
                        ]
                        |> List.filterNotIn ignoredFiles
                }
                |> tee (fun _ -> progress |> output.ProgressAdvance)
            )
            |> List.filter (fun repo ->
                match completeRepository with
                | All -> true
                | OnlyComlete -> repo.Untracked @ repo.NotCommited @ repo.Ignored |> List.isEmpty
                | OnlyIncomplete -> repo.Untracked @ repo.NotCommited @ repo.Ignored |> List.isEmpty |> not
            )

        progress |> output.ProgressFinish

        (* let rec collectIgnored acc = function
            | [] -> acc |> List.groupBy id |> List.sortByDescending (snd >> List.length)
            | repo :: repos ->
                repos |> collectIgnored (acc @ repo.Ignored)

        repositories
        |> collectIgnored []
        |> List.filter (snd >> List.length >> (>=) 1)    // x > 1
        |> List.map (fun (file, occurences) -> sprintf "%s [%i]" file (occurences |> List.length))
        |> FileSystem.writeSeqToFile "output/ignored-minor.txt" *)

        match commandOutput with
        | Stdout output ->
            repositories
            |> List.map (RepositoryBackup.formatToList)
            |> output.Options (sprintf "Repositories[%i]:" (repositories |> List.length))

        | File path ->
            let mutable reposWithError = []

            let rec copyFiles repository sourceDir targetDir = function
                | [] -> ()
                | files ->
                    files
                    |> List.iter (fun file ->
                        let fileTargetPath = targetDir / file

                        if Directory.Exists (sourceDir / file)
                            then
                                sourceDir / file
                                |> Directory.GetFiles
                                |> Seq.toList
                                |> copyFiles repository (sourceDir / file) fileTargetPath
                            else
                                let fileTargetDir = fileTargetPath |> Path.dirName

                                Directory.ensure fileTargetDir

                                try
                                    File.Copy(sourceDir / file, fileTargetPath, true)
                                with
                                | e ->
                                    let message = sprintf "! %s" e.Message

                                    reposWithError <- [repository; message] :: reposWithError
                    )

            output.NewLine()
            let progress = repositories |> List.length |> output.ProgressStart "Backuping repositories ..."

            repositories
            |> List.iter (fun repository ->
                let repositoryBackup = path / repository.Name
                Directory.ensure repositoryBackup

                repository
                |> Json.serialize
                |> FileSystem.writeToFile (repositoryBackup / "backup.json")

                repository.NotCommited |> List.distinct |> copyFiles repository.Name repository.Path (repositoryBackup / "notCommited")
                repository.Untracked |> copyFiles repository.Name repository.Path (repositoryBackup / "untracked")
                repository.Ignored |> copyFiles repository.Name repository.Path (repositoryBackup / "ignored")

                progress |> output.ProgressAdvance
            )

            progress |> output.ProgressFinish

            reposWithError
            |> List.distinct
            |> List.sort
            |> output.Options "Repository with errors:"
