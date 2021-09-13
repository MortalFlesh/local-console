namespace MF.DoIt

open System

[<AutoOpen>]
module Ids =
    type Id = Id of string
    /// It is a UUID value, but doit sometimes use uppercase and sometimes lowercase variant and is strict about it, so it is better to use it just as a string
    type Uuid = Uuid of string

    [<RequireQualifiedAccess>]
    module Id =
        let value (Id id): string = id

    [<RequireQualifiedAccess>]
    module Uuid =
        let ofString = Uuid

        let create () =
            Guid.NewGuid().ToString() |> Uuid

        let value (Uuid id): string = id

type Task = {
    Id: Id
    Uuid: Uuid
    Name: string
    Description: string option
    Focus: Focus
    Deadline: DateTimeOffset option
    SubTasks: SubTask list
    Comments: Comment list
    Project: ProjectReference option
    Context: ContextReference option
    Tags: TagReference list
    Priority: Priority
    // Repeat: string option
    // Reminder: string option
    // AssignTo: string option

    Deleted: DateTimeOffset option
    Trashed: DateTimeOffset option
    Completed: DateTimeOffset option
    Archived: DateTimeOffset option
    Hidden: DateTimeOffset option

    // Meta
    Source: string option
    Created: DateTimeOffset
    Updated: DateTimeOffset
}

and Focus =
    | Date of DateTimeOffset  // today, tomorrow, sheduled
    | Next
    | Someday
    | Waiting

and Priority =
    | Default
    | Low
    | Medium
    | High

and SubTask = {
    Name: string
    Completed: DateTimeOffset option

    // Meta
    Created: DateTimeOffset
    Updated: DateTimeOffset
}

and Comment = {
    Content: string
    Author: string
    AuthorEmail: string

    // Meta
    Created: DateTimeOffset
    Updated: DateTimeOffset
}

and ProjectReference = {
    Id: Uuid
}
and Project = {
    Uuid: Uuid
    Name: string
    Description: string option
    Status: Status

    // Meta
    Created: DateTimeOffset
    Updated: DateTimeOffset
}

and ContextReference = {
    Id: Uuid
}
and Context = {
    Uuid: Uuid
    Name: string

    // Meta
    Created: DateTimeOffset
    Updated: DateTimeOffset
}

and Status =
    | Active
    | Other of string

and TagReference = {
    Name: string
}
and Tag = {
    Uuid: Uuid
    Name: string

    // Meta
    Created: DateTimeOffset
    Updated: DateTimeOffset
}

type Resources = {
    Projects: Project list
    Contexts: Context list
    Tags: Tag list
}

[<RequireQualifiedAccess>]
module Task =
    let id ({ Uuid = id }: Task): Uuid = id

[<RequireQualifiedAccess>]
module ProjectReference =
    let id ({ Id = id }: ProjectReference): Uuid = id

[<RequireQualifiedAccess>]
module ContextReference =
    let id ({ Id = id }: ContextReference): Uuid = id

[<RequireQualifiedAccess>]
module Project =
    let id ({ Uuid = id }: Project): Uuid = id
    let name ({ Name = name }: Project): string = name
    let format ({ Uuid = id; Name = name }: Project): string = $"<c:yellow>{name}</c> (<c:gray>{id}</c>)"

[<RequireQualifiedAccess>]
module Context =
    let id ({ Uuid = id }: Context): Uuid = id
    let name ({ Name = name }: Context): string = name
    let format ({ Uuid = id; Name = name }: Context): string = $"<c:yellow>{name}</c> (<c:gray>{id}</c>)"
