namespace MF.AI

[<RequireQualifiedAccess>]
module Chat =
    open System.Diagnostics
    open Feather.ErrorHandling
    open MF.ConsoleApplication
    open Microsoft.Extensions.AI
    open FSharp.Control
    open FSharp.Data

    type Prompt =
        | UserMessage of string
        | AiChatMessage of ChatMessage
        | History of ChatMessage list

    [<RequireQualifiedAccess>]
    module Prompt =
        open System.Collections.Generic

        let private toList (msg: ChatMessage list): List<ChatMessage> =
            let list = List<ChatMessage>()
            list.AddRange(msg)
            list

        let asChatMessage = function
            | UserMessage msg -> [ ChatMessage(ChatRole.User, msg) ] |> toList
            | AiChatMessage chatMsg -> [ chatMsg ] |> toList
            | History msgs -> msgs |> toList

        let fromHistory (history: ChatMessage seq) =
            history
            |> Seq.toList
            |> History

    type ResponseMessage =
        | Message of string
        | AiChatResponse of ChatResponse

    type private ResponseSchema = JsonProvider<""" { "data": "message" } """>

    [<RequireQualifiedAccess>]
    module ResponseMessage =
        let text = function
            | Message msg -> msg
            | AiChatResponse response ->
                try response.Text |> ResponseSchema.Parse |> fun r -> r.Data
                with _ -> response.Text

    type private Duration = int64

    type private Response = {
        Model: AiModel
        Role: string option
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

        let private baseMessage model role response meta =
            sprintf "<c:green>Ai<%s%s>:</c> %s %s"
                model
                (
                    match role with
                    | Some role -> sprintf "|%s" role
                    | None -> ""
                )
                $"{response}"
                meta

        let message (response: Response) =
            response.Message
            |> ResponseMessage.text
            |> fun text -> baseMessage (AiModel.format response.Model) response.Role text (meta response)

    [<RequireQualifiedAccess>]
    module Response =
        let get<'Type> logError (client: IChatClient) (question: Prompt) = asyncResult {
            let! response =
                client.GetResponseAsync<'Type>(question |> Prompt.asChatMessage)
                |> AsyncResult.ofTaskCatch (fun e ->
                    logError e.Message
                    "Failed to get AI response."
                )
                |> AsyncResult.retryWithExponential logError 1000 3

            return response
        }

        let getStreaming (client: IChatClient) (question: Prompt) =
            client.GetStreamingResponseAsync(question |> Prompt.asChatMessage)
            |> AsyncSeq.ofAsyncEnum
            |> AsyncSeq.map (fun message -> message.Text)

    [<RequireQualifiedAccess>]
    module private Classification =
        let private classificationPrompt categories input =
            [
                "Please classify the following text into one of these categories:"
                categories |> String.concat ", "
                "Provide only the category name as the response."
                ""
                sprintf "Text: %s" input
            ]
            |> String.concat "\n"

        let classify logError (client: IChatClient) (userMessage: string) =
            userMessage
            |> classificationPrompt [ "Technology"; "Gaming"; "Health"; "Other" ]
            |> UserMessage
            |> Response.get<string> logError client

    [<RequireQualifiedAccess>]
    module private Summarization =
        let private summarizationPrompt input =
            [
                "Summarize the following text in 1 concise sentence:"
                ""
                input
            ]
            |> String.concat "\n"

        let summarize logError client text =
            text
            |> summarizationPrompt
            |> UserMessage
            |> Response.get<string> logError client

    [<RequireQualifiedAccess>]
    module private SentimentAnalysis =
        let private sentimentPrompt input =
            [
                "Analyze the sentiment of the following text. Is it Positive, Negative, or Neutral?"
                "Provide only the sentiment as the response."
                ""
                input
            ]
            |> String.concat "\n"

        let analyze logError client text =
            text
            |> sentimentPrompt
            |> UserMessage
            |> Response.get<string> logError client

    type RunSettings = {
        Model: AiModel
        ResponseType: ResponseType list
        SystemMessage: string option
    }

    let private history = ResizeArray<ChatMessage>()

    let run (output: Output) (settings: RunSettings) (client: IChatClient) = asyncResult {
        let mutable isRunning = true
        let logError msg = output.Error("Error: %s", msg)

        let createResponse first total message = {
            Model = settings.Model
            Role = None
            Message = message
            FirstResponse = first
            Total = total
        }

        let show (stopwatch: Stopwatch) response =
            if stopwatch.IsRunning then
                stopwatch.Stop()
            output.Message(Format.message response)

        settings.SystemMessage |> Option.iter (fun sysMsg ->
            history.Add(ChatMessage(ChatRole.System, sysMsg))
        )

        while isRunning do
            let question = output.Ask "User:"

            if question = "exit" then
                isRunning <- false

            elif question = "history" then
                output.Section "Chat History:"
                history
                |> Seq.iter (fun msg ->
                    let role = sprintf "<c:yellow>%s</c>" (msg.Role.ToString())
                    output.Message("%s: %s", role, msg.Text)
                )

            else
                output.Message("<c:gray>Ai<%s>: Thinking ...</c>", settings.Model |> AiModel.format)
                let question = AiChatMessage <| ChatMessage(ChatRole.User, question)
                history.AddRange(question |> Prompt.asChatMessage)

                let mutable firstResponse = None
                let stopwatch = Stopwatch.StartNew()

                let! (response: Response) =
                    match settings.ResponseType with
                    | r when r |> List.contains Streaming ->
                        asyncResult {
                            let wholeResponse = System.Text.StringBuilder()

                            do!
                                history
                                |> Prompt.fromHistory
                                |> Response.getStreaming client
                                |> AsyncSeq.iterAsync (fun message -> async {
                                    if firstResponse.IsNone then
                                        firstResponse <- Some stopwatch.ElapsedMilliseconds

                                    output.Write message
                                    wholeResponse.Append(message) |> ignore
                                })
                                |> AsyncResult.ofAsyncCatch (fun e ->
                                    logError e.Message
                                    "Failed to get streaming AI response."
                                )
                            output.WriteLine ""

                            history.Add(ChatMessage(ChatRole.Assistant, wholeResponse.ToString()))

                            return createResponse firstResponse (Some stopwatch) (Message "")
                        }
                    | _ ->
                        asyncResult {
                            let! (response: ChatResponse<string>) =
                                history
                                |> Prompt.fromHistory
                                |> Response.get<string> logError client
                            history.Add(ChatMessage(ChatRole.Assistant, response.Text))

                            return createResponse None (Some stopwatch) (AiChatResponse response)
                        }

                show stopwatch response

                let perform name action =
                    asyncResult {
                        let stopwatch = Stopwatch.StartNew()
                        let! actionResult =
                            response.Message
                            |> ResponseMessage.text
                            |> action logError client

                        stopwatch.Stop()

                        createResponse None (Some stopwatch) (AiChatResponse actionResult)
                        |> fun response -> { response with Role = Some name }
                        |> show stopwatch
                    }

                do!
                    settings.ResponseType
                    |> List.filter (fun rt -> rt <> Instant && rt <> Streaming)
                    |> List.map (function
                        | Classification -> perform "classification" Classification.classify
                        | Summarization -> perform "summarization" Summarization.summarize
                        | SentimentAnalysis -> perform "sentimentAnalysis" SentimentAnalysis.analyze

                        | _ -> asyncResult { return () }
                    )
                    |> AsyncResult.ofParallelAsyncResults (fun e ->
                        logError e.Message
                        "Failed to get additional AI response."
                    )
                    |> AsyncResult.mapError (List.distinct >> String.concat ", ")
                    |> AsyncResult.ignore

        output.Success "Done"
    }
