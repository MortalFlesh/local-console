namespace MF.AI

module ChatTool =
    open System
    open Feather.ConsoleApplication
    open Microsoft.Extensions.AI
    open System.Collections.Generic

    // Use curried parameters instead of tupled for easier interop with C# Func
    let getCurrentWeather log (location: string): string =
        let temperature = Random.Shared.Next(-10, 35)
        let conditions = [ "sunny"; "cloudy"; "rainy"; "windy"; "snowy" ]
        let condition = conditions.[Random.Shared.Next(conditions.Length)]

        log (sprintf "[Tool] Fetched weather data for %s: %d°C, %s" location temperature condition)

        sprintf "The current weather in %s is %d°C and %s." location temperature condition

    let tools log =
        let getWeatherTool: AITool =
            AIFunctionFactory.Create(
                Func<string, string> (getCurrentWeather log),
                "getCurrentWeather",
                "Get the current weather in a given location"
            )

        [
            getWeatherTool
        ]
