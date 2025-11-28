namespace MF.AI

open System

type EmbeddingSettings = {
    Model: AiEmbeddingModel
    Text: string list
    Compare: bool
}

type EmbeddingResult = ReadOnlyMemory<float32>

[<RequireQualifiedAccess>]
module Embedding =
    open Microsoft.Extensions.AI
    open Feather.ConsoleApplication
    open Feather.ErrorHandling
    open System.Numerics.Tensors

    let generateEmbeddings (output: Output) (client: EmbeddingClient) text: AsyncResult<EmbeddingResult, _> = asyncResult {
        let! (embedding: ReadOnlyMemory<float32>) =
            client.GenerateVectorAsync text
            |> AsyncResult.ofTaskCatch (fun e ->
                output.Error (sprintf "Failed to generate embedding: %s" e.Message)
                "Failed to generate embedding."
            )

        return embedding
    }

    let private compare (emb1: EmbeddingResult) (emb2: EmbeddingResult): float32 =
        TensorPrimitives.CosineSimilarity(emb1.Span, emb2.Span)

    let rec cartesian = function
        | ([],[]) -> []
        | (xs,[]) -> []
        | ([],ys) -> []
        | (x::xs, ys) -> (List.map(fun y -> x,y) ys) @ (cartesian (xs,ys))

    type private EmbeddedText = {
        Text: string
        Embedding: EmbeddingResult
    }

    let generate (output: Output) (settings: EmbeddingSettings) (client: EmbeddingClient) = asyncResult {
        output.Section "Generated Embedding"
        let! (embeddings: (string * EmbeddingResult) list) =
            settings.Text
            |> List.map (fun word -> generateEmbeddings output client word |> AsyncResult.map (fun embedding -> word, embedding))
            |> AsyncResult.ofSequentialAsyncResults (fun e -> "Failed to generate embeddings")
            |> AsyncResult.mapError (List.distinct >> String.concat ", ")

        if settings.Compare then
            output.Section "Cosine Similarity"

            cartesian (embeddings, embeddings)
            |> List.iter (fun ((text1, emb1), (text2, emb2)) ->
                let similarity = compare emb1 emb2
                output.Message("Cosine similarity between <c:yellow>%s</c> and <c:yellow>%s</c>: <c:magenta>%.4f</c>", text1, text2, similarity)
            )

        else
            let _, embedding = embeddings |> List.head
            output.Message("Embedding dimensions: %d", embedding.Span.Length)

            embedding.Span.ToArray()
            |> Array.iter (fun value -> output.Write(sprintf "%.2f, " value))
            output.NewLine()

        return ()
    }
