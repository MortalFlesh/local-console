namespace MF.AI

[<RequireQualifiedAccess>]
module ChatCommand =
    open MF.ConsoleApplication
    open MF.ErrorHandling

    let args = [
        Argument.required "model" "AI model to use."
    ]

    let options = [
        Option.noValue "streaming" (Some "s") "Use streaming response from the AI model."
    ]

    let execute = ExecuteAsyncResult <| fun (input, output) ->
        asyncResult {
            output.Title "AI Chat Command"
            output.Note "Type 'exit' to quit."

            let responseType =
                match input with
                | Input.Option.IsSet "streaming" _ -> Streaming
                | _ -> Instant

            let! model =
                match input |> Input.Argument.asString "model" with
                | Some "gpt-5-mini" -> Ok GPT5Mini
                | _ -> Error "Unsupported model."

            let! client = Configuration.connectClient {
                Model = model
                TokenKey = "GitHubModels:Token"
                ResponseType = responseType
            }

            do!
                client
                |> Chat.run output {
                    Model = model
                    ResponseType = responseType
                }

            return ExitCode.Success
        }
        |> AsyncResult.mapError (CommandError.Message >> ConsoleApplicationError.CommandError)
