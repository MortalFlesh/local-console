namespace MF.Monad

[<RequireQualifiedAccess>]
module Writer =
    open MF.ConsoleApplication

    type Writer<'A> = Writer of ('A * string)

    type Kleisli<'A, 'B, 'C> = ('A -> Writer<'B>) -> ('B -> Writer<'C>) -> ('A -> Writer<'C>)
    let (>=>): Kleisli<'A, 'B, 'C> =
        fun m1 m2 a ->
            let (Writer (b, s1)) = m1 a
            let (Writer (c, s2)) = m2 b

            Writer (c, s1 + s2)

    type Return<'A> = 'A -> Writer<'A>
    let retn: Return<'A> = fun a -> Writer (a, "")

    type UpCase = string -> Writer<string>
    let upCase: UpCase = fun s -> Writer(s.ToUpper(), "upCase ")

    type ToWords = string -> Writer<string list>
    let toWords: ToWords = fun s -> Writer(s.Split " " |> Seq.toList, "toWords ")

    type Process = string -> Writer<string list>
    let runProcess: Process = upCase >=> toWords

    let example = Execute <| fun (input, output) ->
        let (Writer (words, log)) =
            "Hello World"
            |> runProcess

        output.Messages " - " words
        output.Message <| sprintf "<c:dark-yellow>// %s</c>" log

        ExitCode.Success
