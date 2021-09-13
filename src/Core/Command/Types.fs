namespace MF.LocalConsole

type Remote = {
    Name: string
    Url: string
}

[<RequireQualifiedAccess>]
module Remote =
    let format { Name = name; Url = url } =
        sprintf "<c:dark-yellow>%s</c>:%s" name url

type RepositoryBackup = {
    Path: string
    Name: string
    Remotes: Remote list
    Untracked: string list
    NotCommited: string list
    Ignored: string list
}

[<RequireQualifiedAccess>]
module RepositoryBackup =
    let private colorize title = function
        | 0 -> sprintf "<c:gray>%s[0]</c>" title
        | count -> sprintf "<c:yellow>%s[%i]</c>" title count

    let formatToList repository =
        [
            repository.Path |> sprintf "<c:cyan>%s</c>"
            repository.Remotes |> List.map Remote.format |> String.concat "; "
            repository.Untracked |> List.length |> colorize "Untracked"
            repository.NotCommited |> List.length |> colorize "NotCommited"
            repository.Ignored |> List.length |> colorize "Ignored"
        ]
