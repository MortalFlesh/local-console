namespace MF.AI

open System
open Microsoft.Extensions.AI
open Microsoft.Extensions.VectorData

type SearchSettings = {
    Model: AiEmbeddingModel
    Text: string list
    SearchIn: Storage
}

and Storage =
    | InMemory

type Movie () =
    [<VectorStoreKey>]
    member val Key : int = 0 with get, set

    [<VectorStoreData>]
    member val Title: string = "" with get, set

    [<VectorStoreData>]
    member val Description: string = "" with get, set

    [<VectorStoreVector(Dimensions = 384, DistanceFunction = DistanceFunction.CosineSimilarity)>]
    member val Vector : EmbeddingResult = EmbeddingResult() with get, set

[<RequireQualifiedAccess>]
module Movie =
    let movies = [
        Movie(Key = 1, Title = "The Matrix", Description = "A computer hacker learns about the true nature of reality and his role in the war against its controllers.")
        Movie(Key = 2, Title = "Inception", Description = "A thief who steals corporate secrets through dream-sharing technology is given the inverse task of planting an idea into the mind of a CEO.")
        Movie(Key = 3, Title = "Interstellar", Description = "A team of explorers travel through a wormhole in space in an attempt to ensure humanity's survival.")
        Movie(Key = 4, Title = "The Godfather", Description = "The aging patriarch of an organized crime dynasty transfers control of his clandestine empire to his reluctant son.")
        Movie(Key = 5, Title = "Pulp Fiction", Description = "The lives of two mob hitmen, a boxer, a gangster's wife, and a pair of diner bandits intertwine in four tales of violence and redemption.")
    ]

[<RequireQualifiedAccess>]
module VectorSearch =
    open Feather.ErrorHandling
    open Feather.ConsoleApplication
    open FSharp.Control

    module InMemoryVectorStore =
        open Microsoft.SemanticKernel.Connectors.InMemory

        let init (output: Output) (client: EmbeddingClient): AsyncResult<VectorStoreCollection<int, Movie>, _> = asyncResult {
            let vectorStore = new InMemoryVectorStore()
            let movieStore: VectorStoreCollection<int, Movie> = vectorStore.GetCollection<int, Movie>("movies")

            do! movieStore.EnsureCollectionExistsAsync() |> AsyncResult.ofEmptyTaskCatch (fun e ->
                output.Error (sprintf "Failed to ensure collection exists: %s" e.Message)
                "Failed to ensure collection exists."
            )

            do!
                Movie.movies
                |> List.map (fun movie -> asyncResult {
                    let! vector = Embedding.generateEmbeddings output client movie.Description
                    movie.Vector <- vector

                    do!
                        movieStore.UpsertAsync(movie)
                        |> AsyncResult.ofEmptyTaskCatch (fun e ->
                            output.Error (sprintf "Failed to upsert movie %s: %s" movie.Title e.Message)
                            "Failed to upsert movie."
                        )
                })
                |> AsyncResult.ofSequentialAsyncResults (fun e -> "Failed to upsert movies")
                |> AsyncResult.mapError (List.distinct >> String.concat ", ")
                |> AsyncResult.ignore

            return movieStore
        }

    let private searchQuery (output: Output) (client: EmbeddingClient) (collection: VectorStoreCollection<int, 'Data>) f (query: string) = asyncResult {
        let! queryVector = Embedding.generateEmbeddings output client query

        do!
            collection.SearchAsync(queryVector, top = 2)
            |> AsyncSeq.ofAsyncEnum
            |> AsyncSeq.iter f
            |> AsyncResult.ofAsyncCatch (fun e ->
                output.Error (sprintf "Failed to perform vector search: %s" e.Message)
                "Failed to perform vector search."
            )
    }

    let search (output: Output) (settings: SearchSettings) (client: EmbeddingClient) = asyncResult {
        output.Section "Vector Search"
        let query = settings.Text |> String.concat " "
        let searchQuery = searchQuery output client

        do!
            match settings.SearchIn with
            | InMemory ->
                asyncResult {
                    let showMovie (result: VectorSearchResult<Movie>) =
                        let score =
                            match result.Score |> Option.ofNullable with
                            | Some s -> s |> sprintf "<c:magenta>%.4f</c>"
                            | None -> "<c:red>N/A</c>"

                        output.Message("Found Movie: <c:yellow>%s</c> (Score: %s)", result.Record.Title, score)
                        output.Message(" - Description: %s", result.Record.Description)
                        output.Message "-----------------------------"

                    let! (movieStore: VectorStoreCollection<int, Movie>) = InMemoryVectorStore.init output client
                    let searchQuery = searchQuery movieStore showMovie

                    do! searchQuery query

                    let mutable isRunning = true
                    while isRunning do
                        let query = output.Ask "Enter search query (or 'exit' to quit):"

                        if query = "exit" then
                            isRunning <- false
                        else
                            do! searchQuery query
                }
        ()
    }
