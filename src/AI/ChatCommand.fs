namespace MF.AI

[<RequireQualifiedAccess>]
module ChatCommand =
    open System.IO
    open MF.ConsoleApplication
    open Feather.ErrorHandling

    let private models =
        AiModel.All
        |> List.map (AiModel.format >> sprintf "<c:dark-yellow>%s</c>")
        |> String.concat ", "

    let args = [
        Argument.required "model" (sprintf "AI model to use. There are following models available: %s." models)
    ]

    let options = [
        Option.optional "system" None "System message to guide the AI model." None

        Option.noValue "classification" (Some "c") "Use classification response from the AI model."
        Option.noValue "streaming" None "Use streaming response from the AI model."
        Option.noValue "summarization" (Some "s") "Use summarization response from the AI model."
        Option.noValue "sentiment" (Some "t") "Use sentiment analysis response from the AI model."

        Option.noValue "car" None "Use structured data extraction for car listings."
    ]

    let private responseTypes input =
        [
            if Input.Option.isValueSet "classification" input then
                yield Classification

            if Input.Option.isValueSet "streaming" input then
                yield Streaming
            else
                yield Instant

            if Input.Option.isValueSet "summarization" input then
                yield Summarization

            if Input.Option.isValueSet "sentiment" input then
                yield SentimentAnalysis
        ]

    let execute = ExecuteAsyncResult <| fun (input, output) ->
        asyncResult {
            let! model =
                input
                |> Input.Argument.asString "model"
                |> Result.ofOption "AI model is required."
                |> Result.bind AiModel.parse

            use! client = Configuration.connectClient {
                Model = model
                TokenKey = "GitHubModels:Token"
            }

            if Input.Option.isValueSet "car" input
            then
                output.Title "Car Listing Structured Data Extraction"
                do! Structured.run output client

            else
                output.Title "AI Chat Command"
                output.Note "Type 'exit' to quit."

                let responseType = input |> responseTypes
                output.Note ("Using response type: %A", responseType)

                let systemMessage =
                    match input with
                    | Input.Option.IsSet "system" _ ->
                        input
                        |> Input.Option.asString "system"
                        |> Option.map (function
                            | file when file.Contains "." && File.Exists(file) -> File.ReadAllText(file)
                            | msg -> msg
                        )
                    | _ -> None
                systemMessage |> Option.iter (fun _ -> output.Note "Using system message")

                do!
                    client
                    |> Chat.run output {
                        Model = model
                        ResponseType = responseType
                        SystemMessage = systemMessage
                    }

            return ExitCode.Success
        }
        |> AsyncResult.mapError (CommandError.Message >> ConsoleApplicationError.CommandError)
