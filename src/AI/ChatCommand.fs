namespace MF.AI

[<RequireQualifiedAccess>]
module ChatCommand =
    open MF.ConsoleApplication
    open MF.ErrorHandling

    let args = [
        Argument.required "model" "AI model to use."
    ]

    let options = [
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
                match input |> Input.Argument.asString "model" with
                | Some "gpt-5-mini" -> Ok GPT5Mini
                | Some "gpt-4o-mini" -> Ok GPT4OMini
                | _ -> Error "Unsupported model."

            let! client = Configuration.connectClient {
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

                do!
                    client
                    |> Chat.run output {
                        Model = model
                        ResponseType = responseType
                    }

            return ExitCode.Success
        }
        |> AsyncResult.mapError (CommandError.Message >> ConsoleApplicationError.CommandError)
