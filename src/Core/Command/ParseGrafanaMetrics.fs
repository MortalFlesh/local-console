namespace MF.LocalConsole

[<RequireQualifiedAccess>]
module ParseGrafanaMetricsCommand =
    open System
    open System.Collections.Concurrent
    open System.Text.RegularExpressions
    open System.Net.Mail
    open System.IO
    open FSharp.Data
    open MF.ConsoleApplication
    open MF.ErrorHandling
    open MF.Utils

    type NormalizationResponse = JsonProvider<"schema/response.json", SampleIsList=true>

    let arguments = []
    let options = []

    let execute = ExecuteAsyncResult <| fun (input, output) -> asyncResult {
        let phone = input |> Input.Argument.asString "metrics" |> Result.ofOption "Missing input file"

        return ExitCode.Success
    }
