namespace MF.LocalConsole

[<RequireQualifiedAccess>]
module AzureFuncCommand =
    open FSharp.Data
    open System.Collections.Concurrent
    open MF.Utils
    open MF.ConsoleApplication
    open MF.LocalConsole.Console

    let execute: Execute = fun (input, output) ->
        let appName = input |> Input.getArgumentValueAsString "app-name" |> Option.defaultValue "-"
        let functionName = input |> Input.getArgumentValueAsString "function-name" |> Option.defaultValue "-"
        let callCount = input |> Input.getArgumentValueAsString "call-count" |> Option.defaultValue "0" |> int
        let code = input |> Input.getOptionValueAsString "code" |> Option.defaultValue "-"

        output.Table ["app"; "func"; "calls"; "code"] [
            [ appName; functionName; string callCount; code ]
        ]

        output.SubTitle "Starting ..."
        let progress = callCount |> output.ProgressStart "Function calls"

        let baseUrl = sprintf "https://%s.azurewebsites.net/api/%s?code=%s" appName functionName code
        let requests = ConcurrentDictionary<int, string>()

        [
            for i in 0..callCount do
                yield async {
                    let! rawResponse =
                        Http.AsyncRequestString
                            (
                                baseUrl,
                                httpMethod = "POST",
                                headers = [
                                    HttpRequestHeaders.Accept "application/vnd.api+json"
                                    HttpRequestHeaders.ContentType "application/vnd.api+json"
                                ],
                                body = TextRequest
                                    """{
                                        "data": {
                                            "type": "normalization",
                                            "attributes": {
                                                "contact": {
                                                    "email": " JaN.Novak@seznam.cz ",
                                                    "phone": "603 123 122"
                                                }
                                            }
                                        }
                                    }"""
                            )
                        |> Async.Catch

                    progress |> output.ProgressAdvance

                    return
                        match rawResponse with
                        | Choice1Of2 response ->
                            requests.AddOrUpdate(i, response, (fun _ _ -> response)) |> ignore

                            [ string i; "<c:green>Ok</c>" ]
                        | Choice2Of2 e ->
                            let response = sprintf "Error: %A" e.Message
                            requests.AddOrUpdate(i, response, (fun _ _ -> response)) |> ignore

                            [ string i; "<c:red>Error</c>" ]
                }
        ]
        |> Async.Parallel
        |> Async.RunSynchronously
        |> tee (fun _ -> progress |> output.ProgressFinish; output.NewLine())
        |> Seq.toList
        |> output.Table [ "Request"; "Response" ]

        if output.IsDebug() then
            output.Title "Real responses"

            requests
            |> Seq.toList
            |> List.map (fun kv -> [kv.Key |> string; kv.Value])
            |> output.Table [ "Request"; "Response" ]

        output.Success "Done"
        ExitCode.Success
