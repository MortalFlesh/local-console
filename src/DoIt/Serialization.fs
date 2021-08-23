namespace MF.DoIt

open System

type Reference =
    struct
        val mutable Id: string
        val mutable Name: string
        new (id: string, name: string) = { Id = id; Name = name }
    end

type TaskDto = {
    Id: string
    Uuid: string
    Name: string
    Description: string
    Focus: string
    Deadline: Nullable<DateTimeOffset>
    SubTasks: SubTaskDto list
    Comments: CommentDto list
    Project: Nullable<Reference>
    Context: Nullable<Reference>
    Tags: string list
    Priority: string

    Deleted: Nullable<DateTimeOffset>
    Trashed: Nullable<DateTimeOffset>
    Completed: Nullable<DateTimeOffset>
    Archived: Nullable<DateTimeOffset>
    Hidden: Nullable<DateTimeOffset>

    // Meta
    Source: string
    Created: DateTimeOffset
    Updated: DateTimeOffset
}

and SubTaskDto = {
    Name: string
    Completed: Nullable<DateTimeOffset>

    // Meta
    Created: DateTimeOffset
    Updated: DateTimeOffset
}

and CommentDto = {
    Content: string
    Author: string
    AuthorEmail: string

    // Meta
    Created: DateTimeOffset
    Updated: DateTimeOffset
}

type ProjectDto = {
    Uuid: string
    Name: string
    Description: string
    Status: string

    // Meta
    Created: DateTimeOffset
    Updated: DateTimeOffset
}

type ContextDto = {
    Uuid: string
    Name: string

    // Meta
    Created: DateTimeOffset
    Updated: DateTimeOffset
}

type TagDto = {
    Uuid: string
    Name: string

    // Meta
    Created: DateTimeOffset
    Updated: DateTimeOffset
}

type BackupDto = {
    Tasks: TaskDto list
    Projects: ProjectDto list
    Contexts: ContextDto list
    Tags: TagDto list
}

[<RequireQualifiedAccess>]
module Serialization =
    let private serializeOption opt = opt |> Option.defaultValue null

    let private asReference<'Parent, 'Item> (parents: 'Parent list) parentId parentName itemId (item: 'Item option) =
        let parentName item =
            let id = item |> itemId

            parents
            |> List.tryFind (parentId >> ((=) id))
            |> Option.map parentName

        item
        |> Option.map (fun item ->
            Reference(
                item |> itemId |> Uuid.value,
                item |> parentName |> Option.defaultValue "<not-found>"
            )
        )
        |> Option.toNullable

    let serialize (backup: DoItBackup): BackupDto =
        let tasks = backup.Tasks |> List.map (fun task ->
            {
                Id = task.Id |> Id.value
                Uuid = task.Uuid |> Uuid.value
                Name = task.Name
                Description = task.Description |> serializeOption
                Focus =
                    match task.Focus with
                    | Date date -> date.ToString()
                    | Next -> "next"
                    | Someday -> "someday"
                    | Waiting -> "waiting"
                Deadline = task.Deadline |> Option.toNullable
                SubTasks =
                    task.SubTasks
                    |> List.map (fun subTask ->
                        {
                            Name = subTask.Name
                            Completed = subTask.Completed |> Option.toNullable
                            Created = subTask.Created
                            Updated = subTask.Updated
                        }
                    )
                Comments =
                    task.Comments
                    |> List.map (fun comment ->
                        {
                            Content = comment.Content
                            Author = comment.Author
                            AuthorEmail = comment.AuthorEmail
                            Created = comment.Created
                            Updated = comment.Updated
                        }
                    )
                Project = task.Project |> asReference backup.Projects Project.id Project.name ProjectReference.id
                Context = task.Context |> asReference backup.Contexts Context.id Context.name ContextReference.id
                Tags = task.Tags |> List.map (fun tag -> tag.Name)
                Priority =
                    match task.Priority with
                    | High -> "high"
                    | Medium -> "medium"
                    | Low -> "low"
                    | Default -> ""

                Deleted = task.Deleted |> Option.toNullable
                Trashed = task.Trashed |> Option.toNullable
                Completed = task.Completed |> Option.toNullable
                Archived = task.Archived |> Option.toNullable
                Hidden = task.Hidden |> Option.toNullable

                Source = task.Source |> serializeOption
                Created = task.Created
                Updated = task.Updated
            }
        )

        let projects = backup.Projects |> List.map (fun project ->
            {
                Uuid = project.Uuid |> Uuid.value
                Name = project.Name
                Description = project.Description |> serializeOption
                Status =
                    match project.Status with
                    | Active -> "active"
                    | Other other -> other
                Created = project.Created
                Updated = project.Updated
            }
        )

        let contexts = backup.Contexts |> List.map (fun context ->
            let c: ContextDto = {
                Uuid = context.Uuid |> Uuid.value
                Name = context.Name
                Created = context.Created
                Updated = context.Updated
            }
            c
        )

        let tags = backup.Tags |> List.map (fun tag ->
            {
                Uuid = tag.Uuid |> Uuid.value
                Name = tag.Name
                Created = tag.Created
                Updated = tag.Updated
            }
        )

        {
            Tasks = tasks
            Projects = projects
            Contexts = contexts
            Tags = tags
        }
