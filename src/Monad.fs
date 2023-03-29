namespace MF.Monad

[<RequireQualifiedAccess>]
module Writer =
    type Writer<'a> = Writer of ('a * string)

    type Kleisli<'a, 'b, 'c> = ('a -> Writer<'b>) -> ('b -> Writer<'c>) -> ('a -> Writer<'c>)
    let (>=>): Kleisli<'a, 'b, 'c> =
        fun m1 m2 a ->
            let (Writer (b, s1)) = m1 a
            let (Writer (c, s2)) = m2 b

            Writer (c, s1 + s2)

    type Return<'a> = 'a -> Writer<'a>
    let retn: Return<'a> = fun a -> Writer (a, "")

    type UpCase = string -> Writer<string>
    let upCase: UpCase = fun s -> Writer(s.ToUpper(), "upCase ")

    type ToWords = string -> Writer<string list>
    let toWords: ToWords = fun s -> Writer(s.Split " " |> Seq.toList, "toWords ")

    type Process = string -> Writer<string list>
    let runProcess: Process = upCase >=> toWords

    let example ((input, output): MF.ConsoleApplication.IO) =
        let (Writer (words, log)) =
            "Hello World"
            |> runProcess

        output.Messages " - " words
        output.Message <| sprintf "<c:dark-yellow>// %s</c>" log

        ()
