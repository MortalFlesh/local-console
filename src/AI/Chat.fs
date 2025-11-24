namespace MF.AI

[<RequireQualifiedAccess>]
module Chat =
    open MF.ErrorHandling
    open MF.ConsoleApplication
    open Microsoft.Extensions.AI
    open OpenAI.Chat

    let private formatMeta (response: ChatResponse): string =
        match response.Usage with
        | null -> ""
        | usage ->
            sprintf "Tokens - Prompt: %A, Completion: %A"
                usage.InputTokenCount
                usage.OutputTokenCount
            |> sprintf " <c:gray>(%s)</c>"

    let run (output: Output) (client: IChatClient) = asyncResult {
        let mutable isRunning = true
        let model = "AI<gpt-5-mini>" // todo

        while isRunning do
            let question = output.Ask "User:"

            if question = "exit" then
                isRunning <- false

            else
                //let prompt = question |> ChatMessage.CreateUserMessage :> ChatMessage
                output.Message("<c:gray>%s: Thinking ...</c>", model)

                let! response =
                    client.GetResponseAsync (question)
                    |> AsyncResult.ofTaskCatch (fun e -> "Failed to get AI response.")

                // todo - response je porad json, pridat i cas zpracovani

                output.Message("<c:green>%s:</c> %s %s", model, $"{response}", formatMeta response)

        output.Success "Done"
    }
