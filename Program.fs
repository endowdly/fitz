namespace fitz

open System
open System.Timers

module Args =
    open Configuration
    open Argu

    [<CliPrefix(CliPrefix.DoubleDash)>]
    type FitzArguments =
        | Timezones of timezones : string
        | Symbols of Symbols
        | Tics
        | Stretch
        | Colorize
        | Hours12
        | Live
        | [<MainCommand; Unique; First>] Time of TIME : string
        | Version
        | Save

        interface IArgParserTemplate with
            member arg.Usage =
                match arg with
                | Timezones _ ->
                    "timezones to display, comma-seperated (e.g.: 'America/New_York,Europe/London) "
                    + "or named (Office:America/New_York,Home:Europe/London) "
                    + "- for TZ names see TZ database name in https://en.wikipedia.org/wiki/List_of_tz_database_time_zones)"

                | Symbols _ -> $"symbols to use for time blocks"

                | Tics -> "use local time tics on the time axis"
                | Stretch -> "stretch across the terminal at the cost of accuracy"
                | Colorize -> "colorize the symbols"
                | Live -> "display time live (quit via 'q' or 'cfg-cfg'"
                | Hours12 -> "use 12-hour clock"
                | Time _ -> "time to display"
                | Version -> "print version and exit"
                | Save -> "save options to configuration"

    let parser = ArgumentParser.Create<FitzArguments>(programName = "fitz")

    // parseTimeZones parses a comma-seperated list of timezones
    let parseTimeZones (s : string) =
        [ for ss in s.Split(",") ->
              if String.IsNullOrWhiteSpace(ss) then
                  None
              else
                  match ss.Split(":") with
                  | [| name |] -> Some { Name = name.Trim(); TimeZone = name.Trim() }
                  | [| head; tail |] -> Some { Name = head.Trim(); TimeZone = tail.Trim() }
                  | _ -> None ]
        |> List.filter(fun loc -> Option.isSome loc)
        |> List.map(fun loc -> Option.get loc)
        |> Array.ofList

    // Note inputTimeformat and parseTime are wrapped up into System.DateTime.TryParse
    let parseTime (s : string) : DateTime =
        match DateTime.TryParse(s) with
        | true, v -> v
        | false, _ ->
            eprintfn $"ERROR: Cannot parse time: {s} -- fallback to Now"
            DateTime.Now

    let parseFlags (cfg : Config) (s : string []) : Config * DateTime =
        let results = parser.Parse s

        let time =
            if results.TryGetResult(Time).IsNone then
                DateTime.Now
            else
                parseTime(results.GetResult(Time, defaultValue = ""))

        let style =
            { cfg.Style with
                Colorize =
                    if results.TryGetResult(Colorize).IsSome then
                        true
                    else
                        cfg.Style.Colorize
                Symbols = results.GetResult(Symbols, defaultValue = cfg.Style.Symbols) }

        { cfg with
            Style = style
            TimeZones =
                if results.TryGetResult(Timezones).IsNone then
                    cfg.TimeZones
                else
                    parseTimeZones(results.GetResult(Timezones, defaultValue = ""))
            Tics = if results.TryGetResult(Tics).IsSome then true else cfg.Tics
            Stretch =
                if results.TryGetResult(Stretch).IsSome then
                    true
                else
                    cfg.Stretch
            Hours12 =
                if results.TryGetResult(Hours12).IsSome then
                    true
                else
                    cfg.Hours12
            Live = if results.TryGetResult(Live).IsSome then true else cfg.Live },
        time

    let canOverwriteConfig (s : string []) : bool =
        let res = parser.Parse s
        res.TryGetResult(Save).IsSome

    let isVersionFlag (s : string []) : bool =
        let res = parser.Parse s
        res.TryGetResult(Version).IsSome

type ConsoleWindowWatcher() =
    let windowChangedEvent = new Event<_>()
    let timer = new Timer(250)
    let mutable h0 = Console.WindowHeight
    let mutable w0 = Console.WindowWidth

    [<CLIEvent>]
    member __.WindowChangedEvent = windowChangedEvent.Publish

    member __.Watch() =
        timer.Elapsed.Add (fun _ ->
            let w = Console.WindowWidth
            let h = Console.WindowHeight

            if w0 <> w || h0 <> h then
                w0 <- w
                h0 <- h
                windowChangedEvent.Trigger(__))

        timer.Start()

    member __.Dispose() = (__ :> IDisposable).Dispose()

    interface IDisposable with
        member __.Dispose() = timer.Dispose()

module Fitz =

    open System.Reflection

    [<Literal>]
    let Version = "0.1.0"

    // Reflection sucks and I hate it
    let private castAs<'a when 'a : null> (o : obj) =
        match o with
        | :? 'a as res -> res
        | _ -> null

    let private version =
        let o =
            Assembly
                .GetCallingAssembly()
                .GetCustomAttributes(typeof<AssemblyInformationalVersionAttribute>, false)

        let ax = castAs<AssemblyInformationalVersionAttribute []> o

        match Array.tryHead ax with
        | Some v -> v.InformationalVersion
        | None -> Version

    let plotLive cfg =
        let p0 = Console.GetCursorPosition().ToTuple()
        let t0 = DateTime.Now
        let dT = 60_000 - (t0.Second * 1_000 + t0.Millisecond)
        let startTimer = new Timer(dT)
        let timer = new Timer(60_000)
        let watcher = new ConsoleWindowWatcher()

        let update () =
            Console.SetCursorPosition(p0)
            Plot.getPlot cfg DateTime.Now |> Plot.plot

        watcher.WindowChangedEvent.Add (fun _ ->
            Console.Clear()
            update())

        timer.Elapsed.Add(fun _ -> update())

        startTimer.Elapsed.Add (fun _ ->
            update()
            timer.Start()
            startTimer.Dispose()) // Seppeku -- ack!

        startTimer.AutoReset <- false
        update()
        watcher.Watch()
        startTimer.Start()

        { new IDisposable with
            member __.Dispose() =
                timer.Dispose()
                watcher.Dispose() }

    [<EntryPointAttribute>]
    let main argv =

        // Handle our flags and check for help early
        // Argu handles --help as an error, so catch it here
        try
            Args.parser.Parse argv |> ignore
        with
        | :? Argu.ArguParseException as ex ->
            printfn $"{ex.Message}"
            Environment.Exit(1)
        | ex ->
            eprintfn "ERROR:"
            eprintfn $"{ex.Message}"
            Environment.Exit(2)

        if Args.isVersionFlag argv then
            printfn $"fitz {version}"
            Environment.Exit(0)

        // Note on Results
        // Normally I would return Result types and feature ROP here.
        // However, the domain is small, local, and should be very resilient.
        // Because we always have a default value available, we should never need an Error.
        let cfg = Configuration.load
        let settings, time = Args.parseFlags cfg argv

        // Um. Don't hammer user configs
        // Added an option to overwrite with cli args
        if Args.canOverwriteConfig argv then
            Configuration.save settings

        if settings.Live then
            let mutable quit = false

            ConsoleHelper.useAlternateScreenBuffer()
            Console.Clear()
            Console.SetCursorPosition(0, 2)
            Console.CursorVisible <- false

            let run = plotLive settings

            let stop () =
                run.Dispose()
                quit <- true

            Console.CancelKeyPress.Add(fun _ -> stop())

            while not quit do
                let cki = Console.ReadKey(true)

                if cki.Key = ConsoleKey.Q || cki.Key = ConsoleKey.Escape then
                    stop()

            Console.CursorVisible <- true
            ConsoleHelper.useMainScreenBuffer()
        else
            Plot.getPlot settings time |> Plot.plot

        0
