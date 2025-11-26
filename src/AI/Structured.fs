namespace MF.AI

open System.Collections.Generic

[<System.Text.Json.Serialization.JsonConverter(typeof<System.Text.Json.Serialization.JsonStringEnumConverter>)>]
type CarListingType =
    | Sale = 0
    | Lease = 1

type CarDetails() =
    member val Condition: string = null with get, set
    member val Make: string = null with get, set
    member val Model: string = null with get, set
    member val Year: int = 0 with get, set
    member val ListingType: CarListingType = CarListingType.Sale with get, set
    member val Price: int = 0 with get, set
    member val Features: List<string> = null with get, set
    member val Summary: string = null with get, set

[<RequireQualifiedAccess>]
module Structured =
    open System.Diagnostics
    open Feather.ErrorHandling
    open MF.ConsoleApplication
    open Microsoft.Extensions.AI
    open FSharp.Control

    let run (output: Output) client = asyncResult {
        output.Title "Structured Data Extraction"
        let logError msg =
            output.Error ("Error: %s", msg)

        let carListing = [
            "For sale: 2018 Honda Accord EX-L, excellent condition, 30,000 miles, $20,000. Features include leather seats, sunroof, backup camera."
            "Looking to lease a 2020 Tesla Model 3, new condition, 10,000 miles, $500/month. Includes autopilot and premium interior."
            "Selling my 2015 Ford F-150 XLT, good condition, 80,000 miles, $25,000. Comes with towing package and bed liner."
            "Lease offer: 2019 BMW 3 Series 330i, like new, 15,000 miles, $600/month. Features navigation and heated seats."
            "For sale: 2017 Toyota Camry SE, very good condition, 50,000 miles, $18,000. Equipped with Bluetooth and alloy wheels."
        ]

        let prompt listingText =
            [
                "Convert the following car listing into a JSON object matching this F# record type:"
                "type CarDetails = {"
                "    Condition: string // 'New' or 'Used'"
                "    Make: string // Car manufacturer"
                "    Model: string"
                "    Year: int"
                "    ListingType: CarListingType"
                "    Price: int // integer only"
                "    Features: string list // list of features as short strings"
                "    Summary: string // exactly ten words to summarize the listing"
                "}"
                "and this discriminated union:"
                "type CarListingType ="
                "    | Sale"
                "    | Lease"
                ""
                "Here is the listing:"
                listingText
            ]
            |> String.concat "\n"
            |> Chat.UserMessage

        let show (response: ChatResponse<CarDetails>) =
            if output.IsVerbose() then
                output.Section "Extracted Car Details:"

                if output.IsVeryVerbose() then
                    try output.Message ("<c:gray>Full Response: %s</c>", response.Text)
                    with e -> ()

                match response.TryGetResult() with
                | true, details -> output.Message ("<c:cyan>Car</c>:\n%A\n", details)
                | false, _ -> output.Error "Failed to extract car details."

        let! (cars: ChatResponse<CarDetails> list) =
            carListing
            |> List.map (
                prompt
                >> Chat.Response.get<CarDetails> logError client
                >> AsyncResult.tee show
            )
            |> AsyncResult.ofSequentialAsyncResults (fun e ->
                logError e.Message
                "Failed to get car details."
            )
            |> AsyncResult.mapError (List.distinct >> String.concat "; ")

        cars
        |> List.choose (fun r ->
            match r.TryGetResult() with
            | true, details -> Some details
            | false, _ -> None
        )
        |> List.map (fun car -> [
            car.Condition
            car.Make
            car.Model
            sprintf "%d" car.Year
            sprintf "%A" car.ListingType
            sprintf "%d" car.Price
            car.Features |> String.concat ", "
            car.Summary
        ])
        |> output.Table [ "Condition"; "Make"; "Model"; "Year"; "Type"; "Price"; "Features"; "Summary" ]

        return ()
    }
