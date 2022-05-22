namespace fitz

module Helpers =

    open System
    open System.Drawing
    open System.Globalization

    exception TooSmall

    let getByteOfUInt x n = if 0 < n && n <= 4 then x >>> (n - 1) * 8 &&& 0xffu else x

    let (>&) = getByteOfUInt

    let maxStringLength (ls : string list) = ls |> List.sortByDescending(fun s -> s.Length) |> List.head

    let tryGetTerminalWidth =
        try
            let width = Console.WindowWidth

            if width < 24 then raise(TooSmall) else Some width
        with
        | TooSmall -> Some 72
        | _ as ex ->
            eprintfn $"tryGetTerminalWidth fail with {ex.Message}"
            None

    // note maybe do not need with System.Drawing.ColorTranslator
    let convertHexToRgb (s : string) =
        let sHex = if s[0] = '#' then s[1..] else s
        let ns = NumberStyles.HexNumber
        let cc = CultureInfo.CurrentCulture

        match System.UInt32.TryParse(sHex, ns, cc) with
        | false, _ -> 0u, 0u, 0u
        | true, v ->
            let r = v >& 3
            let g = v >& 2
            let b = v >& 1

            r, g, b

    let askUser (s : string) =
        printf $"{s} (y/N): "

        let y = ConsoleKey.Y

        let input =
            try
                // gotz uses a Reader.ReadString here to allow for a 'yes' entry
                // Eh.
                Some <| System.Console.ReadKey().Key
            with
            | _ as ex ->
                eprintf $"AskUser fail with {ex.Message}"
                None

        printfn ""

        match input with
        | Some c -> c = y
        | None -> false

    let rec convertConsoleColorToDrawingColor =
        function
        | ConsoleColor.Black -> Color.Black
        | ConsoleColor.DarkBlue -> Color.DarkBlue
        | ConsoleColor.DarkGreen -> Color.DarkGreen
        | ConsoleColor.DarkCyan -> Color.DarkCyan
        | ConsoleColor.DarkRed -> Color.DarkRed
        | ConsoleColor.DarkMagenta -> Color.DarkMagenta
        | ConsoleColor.DarkYellow -> Color.Brown
        | ConsoleColor.Gray -> Color.Gray
        | ConsoleColor.DarkGray -> Color.DarkGray
        | ConsoleColor.Blue -> Color.Blue
        | ConsoleColor.Green -> Color.Green
        | ConsoleColor.Cyan -> Color.Cyan
        | ConsoleColor.Red -> Color.Red
        | ConsoleColor.Magenta -> Color.Magenta
        | ConsoleColor.Yellow -> Color.Yellow
        | ConsoleColor.White -> Color.White
        | _ ->
            try
                convertConsoleColorToDrawingColor Console.ForegroundColor
            with
            | _ -> Color.Black

module Configuration =

    open System
    open System.IO
    open System.Text

    open FSharp.Json

    // Module-wide encoding
    let Enc = Encoding.UTF8

    // Some types included in configuration.go were... more broadly used
    // Added them as infrastructure types

    [<Struct>]
    type DaySegmentation =
        { [<JsonField("morning")>]
          MorningHour : int
          [<JsonField("day")>]
          DayHour : int
          [<JsonField("evening")>]
          EveningHour : int
          [<JsonField("night")>]
          NightHour : int }

    type Style =
        { [<JsonField("symbols")>]
          Symbols : string
          [<JsonField("colorize")>]
          Colorize : bool
          [<JsonField("day_segments")>]
          DaySegmentation : DaySegmentation
          [<JsonField("coloring")>]
          Coloring : PlotColors }

    type Config =
        { [<JsonField("config_version")>]
          ConfigVersion : string
          [<JsonField("timezones")>]
          Timezones : Location []
          [<JsonField("style")>]
          Style : Style
          [<JsonField("tics")>]
          Tics : bool
          [<JsonField("stretch")>]
          Stretch : bool
          [<JsonField("hours12")>]
          Hours12 : bool
          [<JsonField("live")>]
          Live : bool }

    let defaultConfig =

        let tryGetTimeZoneString (s : string) =
            try
                // ! FindSystemTimeZoneById will only interop with IANA tz databases on .NET 6+
                TimeZoneInfo.FindSystemTimeZoneById(s).Id, None
            with
            // todo add fallback if less than .NET 6+?
            | _ as ex -> "", Some ex

        let ny, _ = tryGetTimeZoneString "America/New_York"
        let london, _ = tryGetTimeZoneString "Europe/Berlin"
        let shanghai, _ = tryGetTimeZoneString "Asia/Shanghai"
        let sydney, _ = tryGetTimeZoneString "Australia/Sydney"


        { ConfigVersion = ConfigVersion
          Timezones =
            [| { Name = "New York"; TZ = ny }
               { Name = "London"; TZ = london }
               { Name = "Shanghai"; TZ = shanghai }
               { Name = "Sydney"; TZ = sydney } |]
          Style =
            { Symbols = ""
              Colorize = false
              DaySegmentation = { MorningHour = 6; DayHour = 8; EveningHour = 18; NightHour = 22 }
              Coloring =
                { StaticColorMorning = "red"
                  StaticColorDay = "yellow"
                  StaticColorEvening = "red"
                  StaticColorNight = "blue"
                  StaticColorForeground = ""
                  DynamicColorMorning = "red"
                  DynamicColorDay = "yellow"
                  DynamicColorEvening = "red"
                  DynamicColorNight = "blue"
                  DynamicColorForeground = ""
                  DynamicColorBackground = "" } }
          Tics = false
          Stretch = true
          Hours12 = true
          Live = false }

    let defaultConfigFile =
        try
            // In go, the xdg package adds the config to LocalAppData
            // Imo, this is a small config and should go in Roaming, but for interop purposes...
            let fAppData = Environment.SpecialFolder.LocalApplicationData
            let appDataDir = Environment.GetFolderPath(fAppData)

            Path.Combine(appDataDir, "gotz\config.json")
        with
        | _ as ex -> failwith ex.Message

    // todo active pattern this
    let save (c : Config) =

        // I am pretty sure FSharp.Json has its own error printing?
        let data = Json.serialize c

        if String.IsNullOrEmpty(data) then
            ()
        else
            try
                File.WriteAllText(defaultConfigFile, data, Enc)
            with
            | _ as ex -> failwith ex.Message

    let saveDefault = save defaultConfig

    // todo add error logging
    // this match combines format.CheckSymbolMode and configuration.validate
    let validate c =
        match c.Style.Symbols with
        | SymbolModeSunMoon -> c
        | SymbolModeMono -> c
        | SymbolModeRectangles -> c
        | _ -> { c with Style = { c.Style with Symbols = SymbolModeDefault } }

    let private (|Contents|AccessDenied|) s =
        try
            Contents(File.ReadAllText(s, Enc))
        with
        | _ -> AccessDenied

    let private (|Json|JsonFail|) s =
        try
            Json(Json.deserialize<Config> s)
        with
        | ex -> JsonFail ex

    // todo error handling
    // idea return result<config, ex>
    let load =

        // If no configuration file exists, create one
        if not (FileInfo defaultConfigFile).Exists then saveDefault

        // Read configuration file
        match defaultConfigFile with
        | Contents v ->

            // Deserialize (unmarshal)
            match v with
            | Json config ->

                // Check version and validate in place
                match config.ConfigVersion with
                | ConfigVersion -> validate config, true
                | _ -> config, false

            | JsonFail _ -> defaultConfig, false

        | AccessDenied -> defaultConfig, false

module Format =

    open System
    open System.Drawing

    open Helpers
    open Configuration

    // need to implement a tiny part of tcell v2 here
    // do not need flags here
    // note may not need all this
    type Attr =
        | AttrBold
        | AttrBlink
        | AttrReverse
        | AttrUnderline
        | AttrDim
        | AttrItalic
        | AttrStrikethrough

    type Style =
        { fg : Color
          bg : Color
          attr : Attr list }

        static member Default =
            { fg = convertConsoleColorToDrawingColor Console.ForegroundColor
              bg = convertConsoleColorToDrawingColor Console.BackgroundColor
              attr = [] }

        member x.Decomposed = x.fg, x.bg, x.attr
        member x.Normal = { x with attr = [] }
        member x.Bold = { x with attr = AttrBold :: x.attr }
        member x.Blink = { x with attr = AttrBlink :: x.attr }
        member x.Dim = { x with attr = AttrDim :: x.attr }
        member x.Italic = { x with attr = AttrItalic :: x.attr }
        member x.Reverse = { x with attr = AttrReverse :: x.attr }
        member x.Underline = { x with attr = AttrUnderline :: x.attr }
        member x.Strikethrough = { x with attr = AttrStrikethrough :: x.attr }
        member x.WithAttr(xs) = { x with attr = xs }
        member x.WithForeground(c) = { x with fg = c }
        member x.WithBackground(c) = { x with bg = c }

    // note probably will not need
    // let namedStaticColors =
    //     [ "black", ColorBlack
    //       "white", ColorWhite
    //       "red", ColorRed
    //       "yellow", ColorYellow
    //       "magenta", ColorMagenta
    //       "green", ColorGreen
    //       "blue", ColorBlue
    //       "cyan", ColorCyan ]
    //     |> Map.ofList

    // todo simplify
    let (|NamedColor|EmptyColor|UnknownColor|) s =
        match ColorTranslator.FromHtml(s) with
        | c when c.IsEmpty -> EmptyColor
        | c when c.IsNamedColor -> NamedColor c
        | _ -> UnknownColor

    let getDynamicColorMap x =

        let getColor =
            function
            | NamedColor c -> c
            | _ -> Color.Black

        let fg0, bg0, _ = Style.Default.Decomposed

        let fg, bg =
            match x.DynamicColorForeground, x.DynamicColorBackground with
            | "", "" -> fg0, bg0
            | "", b -> fg0, getColor b
            | f, "" -> getColor f, bg0
            | f, b -> getColor f, getColor b

        let baseStyle =
            Style
                .Default
                .WithForeground(fg)
                .WithBackground(bg)

        [ ContextNormal, baseStyle
          ContextMorning, baseStyle.WithForeground(getColor x.DynamicColorMorning)
          ContextDay, baseStyle.WithForeground(getColor x.DynamicColorDay)
          ContextEvening, baseStyle.WithForeground(getColor x.DynamicColorEvening)
          ContextNight, baseStyle.WithForeground(getColor x.DynamicColorNight) ]
        |> Map.ofList

    // todo
    let getStaticColorMap x = ()

    // let checkSymbolMode (mode : string) : bool = true <-- vestigal "storyboarding" function, left for posterity

    let (|Morning|Day|Evening|Night|NotHour|) (x, n) =
        match n with
        | _ when x.NightHour <= n || n < x.MorningHour -> Night
        | _ when n < x.DayHour -> Morning
        | _ when n < x.EveningHour -> Day
        | _ when n < x.NightHour -> Evening
        | _ -> NotHour

    // combined the contexts and symbol maps into this function
    let getHourSymbol x n =
        if n < 0 || n > 23 then failwith $"invalid hour: {n}"

        match x.Symbols with
        | SymbolModeRectangles ->

            match x.DaySegmentation, n with
            | Morning
            | Evening -> SymbolRectanglesTwilight
            | Day -> SymbolRectanglesDay
            | Night -> SymbolRectanglesNight
            | NotHour -> SymbolBork

        | SymbolModeSunMoon ->

            match x.DaySegmentation, n with
            | Morning
            | Evening -> SymbolSunMoonTwilight
            | Day -> SymbolSunMoonDay
            | Night -> SymbolSunMoonNight
            | NotHour -> SymbolBork

        | SymbolModeMono -> "#"
        | _ -> failwith $"invalid symbol mode: {x.Symbols}"

module Plot =

    open System

    open Configuration

    type Timeslot = DateTime
    type ContextType = string

    type Plotter =
        { PlotLine : ContextType -> string list -> unit
          PlotString : ContextType -> string -> unit
          TerminalWidth : int
          Now : bool }

    let formatTime b (t : DateTime) =
        // standard formatters are locale specific so we must use custom formatters to enforce a style
        // link https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings

        if b then t.ToString("h:mmtt") else t.ToString("HH:mm")

    let updateTimeNeeded (shown : DateTime) (now : DateTime) : bool = false

    let formatDay (t : DateTime) = t.ToString("ddd dd MMM yyyy")

    // Plot is the main plotting function.
    // it either supports printing to the terminal in a conventional way or
    // it uses ~~tcell~~ <something> for a continously updating plot
    // hmm -- tcell does not seem to do much here
    // could just do a MVC loop
    // idea return Result<unit, Error>
    // todo complete
    let plot (c : Config) (t : DateTime) : unit = ()

    // idea return Result<unit, Error>
    // todo complete
    let plotTime (x : Plotter) (c : Config) (t : DateTime) : unit = ()

    // this returns nothing in gotz, maybe also have it emit a Result<'a, 'b>? k
    // todo complete
    let plotTics (x : Plotter) (b : bool) (ls : Timeslot list) (n : int) : unit = ()

module Args =

    open System
    
    open Configuration 

    open Argu

    [<CliPrefix(CliPrefix.DoubleDash)>]
    type FitzArguments =
        | Timezones of timezones : string
        | Symbols of symbols : string
        | Tics
        | Stretch
        | Colorize
        | Hours12
        | Live
        | [<MainCommand; Unique; First>] Time of TIME:string
        | Version

        interface IArgParserTemplate with
            member arg.Usage =
                match arg with
                | Timezones _ ->
                    "timezones to display, comma-seperated (e.g.: 'America/New_York,Europe/London) "
                    + "or named (Office:America/New_York,Home:Europe/London) "
                    + "- for TZ names see TZ database name in https://en.wikipedia.org/wiki/List_of_tz_database_time_zones)"
                | Symbols _ ->
                    $"symbols to use for time blocks (one of {SymbolModeRectangles}, {SymbolModeSunMoon}, {SymbolModeMono}"
                | Tics -> "use local time tics on the time axis"
                | Stretch -> "stretch across the terminal at the cost of accuracy"
                | Colorize -> "colorize the symbols"
                | Live -> "display time live (quit via 'q' or 'c-c'"
                | Hours12 -> "use 12-hour clock"
                | Time _ -> "time to display"
                | Version -> "print version and exit"
 
    let parser = ArgumentParser.Create<FitzArguments>(programName = "fitz")

    // todo complete
    // todo change name to reflect function
    // idea appSettings/runConfig/runSettings
    let parseFlags (c : Config) (s : string) : Config * DateTime * bool * bool = defaultConfig, DateTime.Now, false, false

    // todo complete
    let parseTimeZones (tz : string) : Location list = []

    // idea use active patter
    let checkTimezoneLocation (tz : string) : bool = false

    // note inputTimeformat and parseTime are wrapped up into System.DateTime.TryParse
    // note do not reinvent the wheel 
    // idea return Result
    let parseTime (s : string) : DateTime =
        match DateTime.TryParse(s) with
        | true, v -> v
        | false, _ -> failwith $"invalid time: {s}"



