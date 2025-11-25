namespace MF.AI

type AiModel =
    | GPT5Mini

[<RequireQualifiedAccess>]
module AiModel =
    let format = function
        | GPT5Mini -> "gpt-5-mini"

type ResponseType =
    | Instant
    | Streaming

type Settings = {
    Model: AiModel
    TokenKey: string
    ResponseType: ResponseType
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

        let options =
            OpenAIClientOptions(
                Endpoint = System.Uri "https://models.github.ai/inference"
            )

        let client =
            match settings.Model with
            | GPT5Mini -> OpenAIClient(credential, options).GetChatClient("openai/gpt-5-mini").AsIChatClient()

        return client
    }
