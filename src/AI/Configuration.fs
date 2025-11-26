namespace MF.AI

type AiModel =
    | GPT5Mini
    | GPT4OMini

type AiFamily =
    | OpenAI

[<RequireQualifiedAccess>]
module AiModel =
    let format = function
        | GPT5Mini -> "gpt-5-mini"
        | GPT4OMini -> "gpt-4o-mini"

    let url = function
        | GPT5Mini
        | GPT4OMini -> "https://models.github.ai/inference"

    let model = function
        | GPT5Mini -> "openai/gpt-5-mini"
        | GPT4OMini -> "openai/gpt-4o-mini"

    let family = function
        | GPT5Mini
        | GPT4OMini -> OpenAI

type ResponseType =
    | Instant
    | Streaming
    | Classification
    | Summarization
    | SentimentAnalysis

type Settings = {
    Model: AiModel
    TokenKey: string
}

[<RequireQualifiedAccess>]
module internal Configuration =
    open MF.ErrorHandling
    open Microsoft.Extensions.AI
    open Microsoft.Extensions.Configuration
    open OpenAI
    open System.ClientModel
    open System.IO
    open FSharp.Data

    /// It required the project has UserSecretsId set in the .fsproj file - local-console-ai-secrets in this case.
    /// And the secret must be stored there (~/.microsoft/usersecrets/local-console-ai-secrets/secrets.json).
    let private getSecret key =
        let config = ConfigurationBuilder().AddUserSecrets().Build()

        match config.[key] with
        | null | "" -> Error "Token is missing in secrets."
        | t -> Ok t

    type private SecretsSchema = JsonProvider<""" { "GitHubModels:Token": "your_token_here" } """>

    let private getSecretFromFile (key: string) = asyncResult {
        let secretsFile = "src/AI/secrets.json"

        if not (File.Exists(secretsFile)) then
            return! Error $"Secrets file '{secretsFile}' not found."

        let content = File.ReadAllText secretsFile
        let secrets = SecretsSchema.Parse content

        return!
            match key with
            | "GitHubModels:Token" -> Ok secrets.GitHubModelsToken
            | _ -> Error $"Secret key '{key}' not found in secrets file."
    }

    let connectClient settings = asyncResult {
        let! token = getSecretFromFile settings.TokenKey

        let credential = ApiKeyCredential token

        let client =
            match settings.Model |> AiModel.family with
            | OpenAI ->
                let options =
                    OpenAIClientOptions(
                        Endpoint = (settings.Model |> AiModel.url |> System.Uri)
                    )

                OpenAIClient(credential, options).GetChatClient(settings.Model |> AiModel.model).AsIChatClient()

        return client
    }
