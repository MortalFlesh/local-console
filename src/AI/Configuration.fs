namespace MF.AI

open System
open System.Net
open Microsoft.Extensions.AI

type AiModel =
    | GPT5Mini
    | GPT4OMini
    | Ollama32

type AiEmbeddingModel =
    | TextEmbedding3Small
    | OllamaEmbedding

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

    let model = function
        | GPT5Mini -> "openai/gpt-5-mini"
        | GPT4OMini -> "openai/gpt-4o-mini"
        | Ollama32 -> "llama3.2"

    let family = function
        | GPT5Mini
        | GPT4OMini -> OpenAI
        | Ollama32 -> Ollama

[<RequireQualifiedAccess>]
module AiEmbeddingModel =
    let All = [ TextEmbedding3Small; OllamaEmbedding ]

    let parse = function
        | "text-embedding-3-small" -> Ok TextEmbedding3Small
        | "ollama-embedding" -> Ok OllamaEmbedding
        | other -> Error <| sprintf "Unknown AI embedding model: %s" other

    let format = function
        | TextEmbedding3Small -> "text-embedding-3-small"
        | OllamaEmbedding -> "ollama-embedding"

    let model = function
        | TextEmbedding3Small -> "openai/text-embedding-3-small"
        | OllamaEmbedding -> "ollama-embedding"

    let family = function
        | TextEmbedding3Small -> OpenAI
        | OllamaEmbedding -> Ollama

[<RequireQualifiedAccess>]
module AiFamily =
    let url = function
        | OpenAI -> Uri "https://models.github.ai/inference"
        | Ollama -> Uri "http://ollama.adun:30080"

type ResponseType =
    | Instant
    | Streaming
    | Classification
    | Summarization
    | SentimentAnalysis

type ChatClientSettings = {
    Model: AiModel
    TokenKey: string
    UseFunctions: bool
}

type EmbeddingClientSettings = {
    Model: AiEmbeddingModel
    TokenKey: string
}

type EmbeddingClient = IEmbeddingGenerator<string, Embedding<float32>>

[<RequireQualifiedAccess>]
module internal Configuration =
    open Feather.ErrorHandling
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

    let connectClient (settings: ChatClientSettings) = asyncResult {
        let! token = getSecretFromFile settings.TokenKey
        let credential = ApiKeyCredential token

        let client =
            match settings.Model |> AiModel.family with
            | OpenAI ->
                let options =
                    OpenAIClientOptions(
                        Endpoint = (OpenAI |> AiFamily.url)
                    )

                OpenAIClient(credential, options).GetChatClient(settings.Model |> AiModel.model).AsIChatClient()
            | Ollama ->
                new OllamaApiClient(
                    Ollama |> AiFamily.url,
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

    let connectEmbeddingGenerator (settings: EmbeddingClientSettings): AsyncResult<EmbeddingClient, _> = asyncResult {
        let! token = getSecretFromFile settings.TokenKey
        let credential = ApiKeyCredential token

        let client: EmbeddingClient =
            match settings.Model |> AiEmbeddingModel.family with
            | OpenAI ->
                let options =
                    OpenAIClientOptions(
                        Endpoint = (OpenAI |> AiFamily.url)
                    )

                OpenAIClient(credential, options).GetEmbeddingClient(settings.Model |> AiEmbeddingModel.model).AsIEmbeddingGenerator()
            | Ollama ->
                (* new OllamaApiClient(
                    Ollama |> AiFamily.url,
                    settings.Model |> AiEmbeddingModel.model
                )
                :> IEmbeddingGenerator *)
                failwithf "Ollama embedding generator not implemented"

        return client
    }
