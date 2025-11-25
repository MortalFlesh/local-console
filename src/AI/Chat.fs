namespace MF.AI

[<RequireQualifiedAccess>]
module Chat =
    open System.Diagnostics
    open MF.ErrorHandling
    open MF.ConsoleApplication
    open Microsoft.Extensions.AI
    open FSharp.Control

    type ResponseMessage =
        | Message of string
        | AiChatResponse of ChatResponse

    type private Duration = int64

    type private Response = {
        Model: AiModel
        Message: ResponseMessage
        FirstResponse: Duration option
        Total: Stopwatch option
    }

    module private Format =
        let private formatDuration (elapsedMilliseconds: Duration) =
            if elapsedMilliseconds < 1000L then
                sprintf "%d ms" elapsedMilliseconds
            elif elapsedMilliseconds < 60000L then
                sprintf "%.2f s" (float elapsedMilliseconds / 1000.0)
            else
                let minutes = elapsedMilliseconds / 60000L
                let seconds = (elapsedMilliseconds % 60000L) / 1000L
                sprintf "%d:%02d min" minutes seconds

        let meta response: string =
            let firstResponse =
                response.FirstResponse
                |> Option.map (fun ms ->
                    sprintf "First: %s" (formatDuration ms)
                )

            let totalDuration =
                response.Total
                |> Option.map (fun stopwatch ->
                    sprintf "Time: %s" (formatDuration stopwatch.ElapsedMilliseconds)
                )

            let usage =
                match response.Message with
                | Message _ -> None
                | AiChatResponse response ->
                    match response.Usage with
                    | null -> None
                    | usage ->
                        sprintf "Tokens - Prompt: %A, Completion: %A"
                            usage.InputTokenCount
                            usage.OutputTokenCount
                        |> Some

            let parts =
                [
                    firstResponse
                    totalDuration
                    usage
                ]
                |> List.choose id

            match parts with
            | [] -> ""
            | parts ->
                parts
                |> String.concat "; "
                |> sprintf " <c:gray>// %s</c>"

        let private baseMessage model response meta =
            sprintf "<c:green>Ai<%s>:</c> %s %s"
                model
                $"{response}"
                meta

        let message response =
            let message text = baseMessage (AiModel.format response.Model) text (meta response)

            match response with
            | { Message = Message text } -> message text
            | { Message = AiChatResponse response } -> message response

    [<RequireQualifiedAccess>]
    module private Response =
        let get (client: IChatClient) (question: string) = asyncResult {
            let! response =
                client.GetResponseAsync(question)
                |> AsyncResult.ofTaskCatch (fun e -> "Failed to get AI response.")

            return response
        }

        let getStreaming (client: IChatClient) (question: string) =
            client.GetStreamingResponseAsync(question)
            |> AsyncSeq.ofAsyncEnum
            |> AsyncSeq.map (fun message -> message.Text)

    type RunSettings = {
        Model: AiModel
        ResponseType: ResponseType
    }

    let run (output: Output) settings (client: IChatClient) = asyncResult {
        let mutable isRunning = true
        let createResponse first total message = {
            Model = settings.Model
            Message = message
            FirstResponse = first
            Total = total
        }

        while isRunning do
            let question = output.Ask "User:"

            if question = "exit" then
                isRunning <- false

            else
                output.Message("<c:gray>%s: Thinking ...</c>", settings.Model |> AiModel.format)
                let stopwatch = Stopwatch.StartNew()
                let mutable firstResponse = None

                if settings.ResponseType = Streaming then
                    do!
                        question
                        |> Response.getStreaming client
                        |> AsyncSeq.iterAsync (fun message -> async {
                            if firstResponse.IsNone then
                                firstResponse <- Some stopwatch.ElapsedMilliseconds

                            output.Write message
                        })
                        |> AsyncResult.ofAsyncCatch (fun e -> "Failed to get streaming AI response.")

                    stopwatch.Stop()
                    output.WriteLine(Format.message (createResponse firstResponse (Some stopwatch) (Message "")))
                else
                    let! response = Response.get client question

                    stopwatch.Stop()
                    output.Message(Format.message (createResponse None (Some stopwatch) (AiChatResponse response)))

        output.Success "Done"
    }
