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
                | Live -> "display time live (quit via 'q' or 'c-c'"
                | Hours12 -> "use 12-hour clock"
                | Time _ -> "time to display"
                | Version -> "print version and exit"
                | Save -> "save options to configuration"

    let parser = ArgumentParser.Create<FitzArguments>(programName = "fitz")

    // note gotz checks the incoming string before and after it is loaded as a config type
    // maybe just let the config logic handle this?

    // parseTimeZones parses a comma-seperated list of timezones
    // idea return Result and bind with parseTime
    let parseTimeZones (s : string) =
        [ for ss in s.Split(",") -> 
              match String.IsNullOrWhiteSpace(ss) with 
              | true -> None 
              | false -> 
                  match ss.Split(":") with
                  | [| name |] -> Some { Name = name.Trim(); TimeZone = name.Trim() }
                  | [| head; tail |] -> Some { Name = head.Trim(); TimeZone = tail.Trim() }
                  | _ -> None ] 
        |> List.filter(fun loc -> Option.isSome loc)
        |> List.map(fun loc -> Option.get loc)
        |> Array.ofList

    // note inputTimeformat and parseTime are wrapped up into System.DateTime.TryParse
    // idea return Result
    let parseTime (s : string) : DateTime =
        match DateTime.TryParse(s) with
        | true, v -> v
        | false, _ -> failwith $"invalid time: {s}"

    let parseFlags (c : Config) (s : string []) : Config * DateTime =
        let results = parser.Parse s

        let time =
            if results.TryGetResult(Time).IsNone then
                DateTime.Now
            else
                parseTime(results.GetResult(Time, defaultValue = ""))

        let style =
            { c.Style with
                Colorize =
                    if results.TryGetResult(Colorize).IsSome then
                        true
                    else
                        c.Style.Colorize
                Symbols = results.GetResult(Symbols, defaultValue = c.Style.Symbols) }

        { c with
            Style = style
            TimeZones =
                if results.TryGetResult(Timezones).IsNone then
                    c.TimeZones
                else
                    parseTimeZones(results.GetResult(Timezones, defaultValue = ""))
            Tics = if results.TryGetResult(Tics).IsSome then true else c.Tics
            Stretch =
                if results.TryGetResult(Stretch).IsSome then
                    true
                else
                    c.Stretch
            Hours12 =
                if results.TryGetResult(Hours12).IsSome then
                    true
                else
                    c.Hours12
            Live = if results.TryGetResult(Live).IsSome then true else c.Live },
        time

    let canOverwriteConfig (s : string []) : bool =
        let res = parser.Parse s
        if res.TryGetResult(Save).IsSome then true else false

type ConsoleWindowWatcher() = 
    let windowChangedEvent = new Event<_>()
    let timer = new Timer(250)
    let mutable h0 = Console.WindowHeight
    let mutable w0 = Console.WindowWidth

    [<CLIEvent>]
    member __.WindowChangedEvent = windowChangedEvent.Publish

    member __.Watch() = 
        timer.Elapsed.Add(fun _ ->        
            let w = Console.WindowWidth
            let h = Console.WindowHeight

            if w0 <> w || h0 <> h then
                w0 <- w; h0 <- h
                windowChangedEvent.Trigger(__)
            )
        timer.Start()

    member __.Dispose() = (__ :> IDisposable).Dispose()

    interface IDisposable with 
        member __.Dispose() = timer.Dispose() 

module Fitz =

    [<Literal>]
    let Version = "0.0.1"
 
    let plotLive c =
        let p0 = Console.GetCursorPosition().ToTuple() 
        let t0 = DateTime.Now
        let dT = 60_000 - (t0.Second * 1_000 + t0.Millisecond) 
        let startTimer = new Timer(dT)
        let timer = new Timer(60_000) 
        let watcher = new ConsoleWindowWatcher() 
        let update () = 
            Console.SetCursorPosition(p0)
            Plot.getPlot c DateTime.Now |> Plot.plot

        watcher.WindowChangedEvent.Add(fun _ -> Console.Clear(); update())
        timer.Elapsed.Add(fun e -> update())
        startTimer.Elapsed.Add(fun e -> 
            update()
            timer.Start()
            startTimer.Dispose()) 
        startTimer.AutoReset <- false 
        update(); watcher.Watch(); startTimer.Start()  
        
        { new IDisposable with 
            member __.Dispose() = 
                timer.Dispose()
                watcher.Dispose() } 

    [<EntryPointAttribute>]
    let main argv =

        // get configuration
        let cfg, err = Configuration.load
        // if err block...
 
        if err then printfn "config error"

        // parse flags
        let settings, time = Args.parseFlags cfg argv 

        // update config
        // Um. Don't hammer user configs.
        // Maybe add a flag or something, but this should not be default behavior.
        match Args.canOverwriteConfig argv with
        | false -> ()
        | true -> Configuration.save cfg 

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
                if cki.Key = ConsoleKey.Q || cki.Key = ConsoleKey.Escape then stop()

            Console.CursorVisible <- true
            ConsoleHelper.useMainScreenBuffer()
        else
            Plot.getPlot settings time |> Plot.plot

        // if live then useMainScreenBuffer
        0
