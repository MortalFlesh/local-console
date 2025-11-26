namespace MF.AI

open System
open System.Net

type AiModel =
    | GPT5Mini
    | GPT4OMini
    | Ollama32

type AiFamily =
    | OpenAI
    | Ollama

[<RequireQualifiedAccess>]
module AiModel =
    let All = [ GPT5Mini; GPT4OMini; Ollama32 ]

    let parse = function
        | "gpt-5-mini" -> Ok GPT5Mini
        | "gpt-4o-mini" -> Ok GPT4OMini
        | "ollama-3.2" -> Ok Ollama32
        | other -> Error <| sprintf "Unknown AI model: %s" other

    let format = function
        | GPT5Mini -> "gpt-5-mini"
        | GPT4OMini -> "gpt-4o-mini"
        | Ollama32 -> "ollama-3.2"

    let url = function
        | GPT5Mini
        | GPT4OMini -> Uri "https://models.github.ai/inference"
        | Ollama32 -> Uri "http://ollama.adun:30080"

    let model = function
        | GPT5Mini -> "openai/gpt-5-mini"
        | GPT4OMini -> "openai/gpt-4o-mini"
        | Ollama32 -> "llama3.2"

    let family = function
        | GPT5Mini
        | GPT4OMini -> OpenAI
        | Ollama32 -> Ollama

type ResponseType =
    | Instant
    | Streaming
    | Classification
    | Summarization
    | SentimentAnalysis

type Settings = {
    Model: AiModel
    TokenKey: string
    UseFunctions: bool
}

[<RequireQualifiedAccess>]
module internal Configuration =
    open Feather.ErrorHandling
    open Microsoft.Extensions.AI
    open Microsoft.Extensions.Configuration
    open OpenAI
    open OllamaSharp
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
                        Endpoint = (settings.Model |> AiModel.url)
                    )

                OpenAIClient(credential, options).GetChatClient(settings.Model |> AiModel.model).AsIChatClient()
            | Ollama ->
                new OllamaApiClient(
                    settings.Model |> AiModel.url,
                    settings.Model |> AiModel.model
                )
                :> IChatClient

        let client =
            if settings.UseFunctions then
                ChatClientBuilder(client).UseFunctionInvocation().Build()
            else
                client

        return client
    }
