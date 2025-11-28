namespace MF.AI

[<RequireQualifiedAccess>]
module EmbeddingCommand =
    open System.IO
    open Feather.ConsoleApplication
    open Feather.ErrorHandling

    let private models =
        AiEmbeddingModel.All
        |> List.map (AiEmbeddingModel.format >> sprintf "<c:dark-yellow>%s</c>")
        |> String.concat ", "

    let args = [
        Argument.required "model" (sprintf "AI embedding model to use. There are following models available: %s." models)
        Argument.requiredArray "input" "Input text or file to generate embedding for."
    ]

    let options = [
        Option.noValue "compare" (Some "c") "Compare embeddings of all input texts."
        Option.noValue "search" (Some "s") "Search input in vector store."
    ]

    let execute = ExecuteAsyncResult <| fun (input, output) ->
        asyncResult {
            let! model =
                input
                |> Input.Argument.asString "model"
                |> Result.ofOption "AI model is required."
                |> Result.bind AiEmbeddingModel.parse

            let! client = Configuration.connectEmbeddingGenerator {
                Model = model
                TokenKey = "GitHubModels:Token"
            }

            let compareWords = Input.Option.isValueSet "compare" input
            let searchInVectorStore = Input.Option.isValueSet "search" input

            output.Title "AI Embedding Command"

            let text =
                input
                |> Input.Argument.asList "input"
                |> List.map (function
                    | file when file.Contains "." && File.Exists(file) -> File.ReadAllText(file)
                    | msg -> msg
                )

            match searchInVectorStore with
            | true ->
                do! client |> VectorSearch.search output {
                    Model = model
                    Text = text
                    SearchIn = InMemory
                }
            | _ ->
                do! client |> Embedding.generate output {
                    Model = model
                    Text = text
                    Compare = compareWords
                }

            return ExitCode.Success
        }
        |> AsyncResult.mapError (CommandError.Message >> ConsoleApplicationError.CommandError)
