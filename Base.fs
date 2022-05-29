namespace fitz

module Helper =
    open System

    let rec times (from : DateTime) (toTime : DateTime) =
        seq {
            if from <= toTime then
                yield from
                yield! times (from.AddTicks(1)) toTime
        }

module Style =

    open System

    let rec convertConsoleColorToColor =
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
                convertConsoleColorToColor Console.ForegroundColor
            with
            | _ -> Color.Black

    let empty =
        { Foreground = convertConsoleColorToColor Console.ForegroundColor
          Background = convertConsoleColorToColor Console.BackgroundColor
          Attributes = [] }

    let withForeground color style = { style with Foreground = color }
    let withBackground color style = { style with Background = color }
    let normal style = { style with Attributes = [] }
    let addAttribute attr style = { style with Attributes = attr :: style.Attributes }

    // note SGR: select graphic rendition parameters
    // link -> https://en.wikipedia.org/wiki/ANSI_escape_code#SGR_(Select_Graphic_Rendition)_parameters

    let colorSgrFromRgb layer (r, g, b) : SgrString =
        match layer with
        | Foreground -> $"38;2;{r};{g};{b}"
        | Background -> $"48;2;{r};{g};{b}"

    let rgbFromColor (color : Color) = color.R, color.G, color.B
    let fgSgrFromRgb = colorSgrFromRgb Foreground
    let bgSgrFromRgb = colorSgrFromRgb Background
    let fgSgrFromColor = rgbFromColor >> fgSgrFromRgb
    let bgSgrFromColor = rgbFromColor >> bgSgrFromRgb

    let sgrFromAttr =
        function
        | Bold -> "1"
        | Dim -> "2"
        | Italic -> "3"
        | Underline -> "4"
        | Blink -> "5"
        | Reverse -> "7"
        | Strikethrough -> "9"

    let sgrFromStyle style =
        let attrs =
            style.Attributes
            |> List.map sgrFromAttr
            |> String.concat ";"

        let fg = fgSgrFromColor style.Foreground
        let bg = bgSgrFromColor style.Background

        [ fg; bg; attrs ]

    let csFromSgr ls : AnsiCommandString = "\u001b[" + (String.concat ";" ls).TrimEnd(';') + "m"
    let csFromStyle = sgrFromStyle >> csFromSgr
    let reset = printf $"{Literal.AnsiReset}"

module Cell =

    let make char style = { Char = char; Style = style }
    let empty = make (char Literal.space) Style.empty
    let withStyle style cell = { cell with Style = style }
    let print cell = printf $"{Style.csFromStyle cell.Style}{cell.Char}{Literal.AnsiReset}"

module Segment =

    let make n cell = Array.create n cell
    let empty n = make n Cell.empty
    let withStyle style cells = Array.map (Cell.withStyle style) cells

    let fromString s =
        s
        |> Seq.toArray
        |> Array.map(fun ch -> Cell.make ch Style.empty)

    let print cells =
        Array.iter Cell.print cells
        printf "\n"

    let add cells segment =
        let head = List.ofArray segment
        let tail = List.ofArray cells

        head @ tail |> Array.ofList

    let addStr str segment = add (fromString str) segment

    let insertCells pos newCells (cells : Cell []) =
        try
            cells[pos .. (pos + (Array.length newCells) - 1)] <- newCells
            cells
        with
        | _ -> cells

    let insertCellsInPlace pos newCells (cells : Cell []) =
        try
            cells[pos .. (pos + (Array.length newCells) - 1)] <- newCells
        with
        | _ -> ()


    let centered str n =
        let insert = fromString str
        let into = empty n
        let pos = (n / 2) - ((String.length str) / 2) - 1

        insertCells pos insert into

module Block =

    // idea may need "word wrap"

    let make m n = Array2D.create m n Cell.empty

    let withStyle style block = Array2D.map (fun cell -> Cell.withStyle style cell) block

    let print block =
        [ 0 .. (Array2D.length1 block) - 1 ]
        |> List.map(fun row -> block[row, *])
        |> List.iter(fun row -> Segment.print row)

    // done todo add row
    let add row ls =
        match ls with
        | [] -> [ row ]
        | _ -> row :: ls

    // done todo make from rows
    let fromRows (ls : Cell [] list) =

        let m = List.length ls
        let n = List.maxBy (fun row -> Array.length row) ls |> Array.length
        let block = make m n

        [ 0 .. m - 1 ]
        |> List.iter (fun row ->
            let newRow = Segment.insertCells 0 ls[row] block[row, *]

            block[row, *] <- newRow)

        block

module Configuration =

    open System
    open System.Drawing
    open System.IO
    open System.Text

    open FSharp.Json

    [<Literal>]
    let ConfigVersion = "1.0.0"

    // Module-wide encoding
    let Enc = Encoding.UTF8

    type Symbols =
        | [<JsonField("rectangles")>] Rectangles
        | [<JsonField("mono")>] Mono
        | [<JsonField("sun-moon")>] SunMoon

    [<Struct>]
    type PlotColors =
        { [<JsonField("color_morning")>]
          ColorMorning : string
          [<JsonField("color_day")>]
          ColorDay : string
          [<JsonField("color_evening")>]
          ColorEvening : string
          [<JsonField("color_night")>]
          ColorNight : string
          [<JsonField("foreground")>]
          Foreground : string
          [<JsonField("background")>]
          Background : string }

    [<Struct>]
    type ConfigLocation =
        { [<JsonField("name")>]
          Name : string
          [<JsonField("timezone")>]
          TimeZone : string }

    [<Struct>]
    type DaySegmentation =
        { [<JsonField("morning")>]
          HourMorning : int
          [<JsonField("day")>]
          HourDay : int
          [<JsonField("evening")>]
          HourEvening : int
          [<JsonField("night")>]
          HourNight : int }

    type Style =
        { [<JsonField("symbols")>]
          Symbols : Symbols
          [<JsonField("colorize")>]
          Colorize : bool
          [<JsonField("day_segments")>]
          DaySegmentation : DaySegmentation
          [<JsonField("colors")>]
          Colors : PlotColors }

    type Config =
        { [<JsonField("config_version")>]
          ConfigVersion : string
          [<JsonField("timezones")>]
          TimeZones : ConfigLocation []
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
        { ConfigVersion = ConfigVersion
          TimeZones =
            [| { Name = "New York"; TimeZone = "America/New_York" }
               { Name = "Berlin"; TimeZone = "Europe/Berlin" }
               { Name = "Shanghai"; TimeZone = "Asia/Shanghai" }
               { Name = "Sydney"; TimeZone = "Australia/Sydney" } |]
          Style =
            { Symbols = Rectangles
              Colorize = false
              DaySegmentation = { HourMorning = 6; HourDay = 8; HourEvening = 18; HourNight = 22 }
              Colors =
                { ColorMorning = "red"
                  ColorDay = "yellow"
                  ColorEvening = "red"
                  ColorNight = "blue"
                  Foreground = ""
                  Background = "" } }
          Tics = false
          Stretch = false
          Hours12 = false
          Live = false }

    let defaultConfigFile =
        try
            // note on the difference between known folder calls
            // in go, the xdg package adds the config to LocalAppData for Win and xdg_config_home for unix
            // in .net core, to link to xdg_config_home, use ApplicationData
            // ApplicationData also links to AppData/Roaming, which is the correct heirarchy for
            // a small, portable config like this, according to Microsoft
            // in short: ardrg/xdg gets it **wrong**
            let fAppData = Environment.SpecialFolder.ApplicationData
            let appDataDir = Environment.GetFolderPath(fAppData)

            Path.Combine(appDataDir, "fitz\config.json")
        with
        | _ as ex -> failwith ex.Message

    let private (|Json|JsonFail|) data =
        try
            Json(Json.serialize data)
        with
        | ex -> JsonFail ex

    // done todo active pattern this
    let save (c : Config) =
        match c with
        | Json s ->
            try
                File.WriteAllText(defaultConfigFile, s, Enc)
            with
            | :? DirectoryNotFoundException ->
                let fi = FileInfo defaultConfigFile
                let di = DirectoryInfo fi.DirectoryName

                if not di.Exists then
                    try
                        Directory.CreateDirectory(di.FullName) |> ignore
                        File.WriteAllText(defaultConfigFile, s, Enc)
                    with
                    | _ as ex -> failwith ex.Message

            | _ as ex -> failwith ex.Message

        | JsonFail ex -> failwith ex.Message

    let saveDefault = save defaultConfig

    let private (|Contents|AccessDenied|) s =
        try
            Contents(File.ReadAllText(s, Enc))
        with
        | _ -> AccessDenied

    let private (|Config|ConfigFail|) s =
        try
            Config(Json.deserialize<Config> s)
        with
        | ex -> ConfigFail ex

    // todo better error handling
    // idea return result<config, ex>
    let load =

        // If no configuration file exists, create one
        if not (FileInfo defaultConfigFile).Exists then saveDefault

        // Read configuration file
        match defaultConfigFile with
        | Contents v ->

            // Deserialize (unmarshal)
            match v with
            | Config data ->

                // Check version and validate in place
                match data.ConfigVersion with
                | ConfigVersion -> data, true
                | _ -> data, false

            | ConfigFail _ ->
                eprintfn "Could not deserialize config, using default"
                defaultConfig, false

        | AccessDenied ->
            eprintfn "Could not load config file"
            defaultConfig, false

    // These functions will let us turn our json unfriedly types into core types
    // idea maybe bind to a Result for parseTimeZone
    let tryGetTimeZone s =
        try
            TimeZoneInfo.FindSystemTimeZoneById(s)
        with
        | _ ->
            eprintfn $"Could not find timezone '{s}, defaulting to system timezone"
            TimeZone.Local

    let configLocationToLocation x : Location = { Name = x.Name; TimeZone = tryGetTimeZone x.TimeZone }

    let tryGetColor =
        function
        | "" -> Style.convertConsoleColorToColor Console.ForegroundColor
        | s ->
            try
                ColorTranslator.FromHtml(s)
            with
            | _ ->
                eprintfn $"{s} is not a known color or html hex color, using default color"
                Style.convertConsoleColorToColor Console.ForegroundColor

module Plot =

    open System

    open Configuration

    let SymbolRectanglesDay = "█"
    let SymbolRectanglesTwilight = "▒"
    let SymbolRectanglesNight = Literal.space
    let SymbolSunMoonDay = "☀"
    let SymbolSunMoonTwilight = "☼"
    let SymbolSunMoonNight = "☾"
    let SymbolMono = "#"
    let SymbolBork = "?"


    let getContext config n =
        let x = config.Style.DaySegmentation

        match n with
        | _ when x.HourNight <= n || n < x.HourMorning -> Night
        | _ when n < x.HourDay -> Morning
        | _ when n < x.HourEvening -> Day
        | _ when n < x.HourNight -> Evening
        | _ -> Normal

    // combined the contexts and symbol maps into this function
    let getHourSymbolString config n =

        match config.Style.Symbols with
        | Rectangles ->

            match getContext config n with
            | Morning
            | Evening -> SymbolRectanglesTwilight
            | Day -> SymbolRectanglesDay
            | Night -> SymbolRectanglesNight
            | Normal -> SymbolBork

        | SunMoon ->

            match getContext config n with
            | Morning
            | Evening -> SymbolSunMoonTwilight
            | Day -> SymbolSunMoonDay
            | Night -> SymbolSunMoonNight
            | Normal -> SymbolBork

        | Mono -> "#"

    let getHourSymbol config n = getHourSymbolString config n |> char

    let formatTime b (t : DateTime) =
        // standard formatters are locale specific so we must use custom formatters to enforce a style
        // link https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings

        if b then t.ToString("h:mmtt") else t.ToString("HH:mm")

    let formatDay (t : DateTime) = t.ToString("ddd dd MMM yyyy")

    let formatDateTime b (t : DateTime) = (formatDay t) + " " + (formatTime b t)

    let getStyleFromContext (withConfig : Config) context =
        let fg, bg = withConfig.Style.Colors.Foreground, withConfig.Style.Colors.Background

        match context with
        | Normal ->

            let s0 = Style.empty

            match fg, bg with
            | "", "" -> s0
            | fg, "" -> Style.withForeground (tryGetColor fg) s0
            | "", bg -> Style.withBackground (tryGetColor bg) s0
            | fg, bg ->
                s0
                |> Style.withForeground(tryGetColor fg)
                |> Style.withBackground(tryGetColor bg)

        | Morning -> Style.withForeground (tryGetColor withConfig.Style.Colors.ColorMorning) Style.empty

        | Day -> Style.withForeground (tryGetColor withConfig.Style.Colors.ColorDay) Style.empty

        | Evening -> Style.withForeground (tryGetColor withConfig.Style.Colors.ColorEvening) Style.empty

        | Night -> Style.withForeground (tryGetColor withConfig.Style.Colors.ColorNight) Style.empty


    let toTimeZone t tz =
        try
            TimeZoneInfo.ConvertTimeFromUtc(t, tz)
        with
        | _ ->
            eprintfn $"Could not convert to {tz.ToString()} using local"
            t


    // todo add "grouped" display
    (*
        eh's idea

        sort times save local
        spread out into day groups

        where a group is a
            desc time
            lineplot

        marker line
        local group

        day 0 (today or yesterday)
        group 0
        group 1

        day 1 (today or tomorrow)
        group 2
        group 3
    *)
    let plotTime (c : Configuration.Config) (t : DateTime) =
        // let plotTime (t : DateTime) =
        let utc = t.ToUniversalTime()

        let style =
            match c.Style.Colorize with
            | false -> (fun _ -> Style.empty)
            | true -> getStyleFromContext c

        let w =
            if c.Stretch then
                Console.WindowWidth
            else
                Console.WindowWidth / 24 * 24

        let hours = 24.0
        let nowSlot = (w / 2) - 1
        let slotMins = hours * 60.0 / (float w)
        let offsetMins = slotMins * (float w) / 2.0

        // note determining this programmatically can take a bit of time
        // this has to generate and check every tick in a second -- we can speed up execution
        // by shrinking the check range or eliminate it by passing a boolean like: plotTime c t b
        // just have Args.parseFlags emit a bool at the end to pass to here
        // note the longest time will be when an arbitrary time is passed
        // Seq.contains (lazy) bails if it finds a generated match so 'now' execution will be fast
        let markerStr =
            let plusOne = t.AddSeconds(1.0)
            let inTime = Helper.times t plusOne |> Seq.contains DateTime.Now

            if inTime then "now" else "time"

        let markerStrSeg = Segment.fromString markerStr
        let markerTimeSeg = Segment.fromString(formatTime c.Hours12 t)

        let markerSeg =
            Segment.centered "v" w
            |> Segment.insertCells (nowSlot - markerStr.Length - 1) markerStrSeg
            |> Segment.insertCells (nowSlot + 2) markerTimeSeg
            |> Segment.withStyle(style Normal)

        let timezones : Location list =
            c.TimeZones
            |> Array.map configLocationToLocation
            |> List.ofArray
            |> (fun ls -> { Name = "Local"; TimeZone = TimeZoneInfo.Local } :: ls)

        let descriptionLength =
            timezones
            |> List.map(fun loc -> loc.Name)
            |> List.maxBy(fun s -> String.length s)
            |> String.length

        let descriptionLengthPlus2 = descriptionLength + 2

        Segment.print markerSeg

        timezones
        |> List.iter (fun timezone ->
            let time = formatDateTime c.Hours12 (toTimeZone utc timezone.TimeZone)

            let desc =
                (timezone.Name + ":")
                    .PadRight(descriptionLengthPlus2)
                + time

            let descLen = String.length desc
            let descCells = Segment.fromString desc

            let head =
                let s0 = Segment.empty w |> Segment.insertCells 0 descCells

                if (descLen - 1) < nowSlot then
                    s0
                    |> Segment.insertCells nowSlot (Segment.fromString Literal.bar)
                    |> Segment.withStyle(style Normal)
                else
                    s0 |> Segment.withStyle(style Normal)

            let tail =
                Segment.empty w
                |> Array.mapi (fun x cell ->
                    let timeSlot = utc.AddMinutes((float x) * slotMins - offsetMins)
                    let tzTime = toTimeZone timeSlot timezone.TimeZone

                    { cell with
                        Char = getHourSymbol c tzTime.Hour
                        Style = style(getContext c tzTime.Hour) })
                |> Segment.insertCells
                    nowSlot
                    (Segment.fromString Literal.bar
                     |> Segment.withStyle(style Normal))

            // todo need better names for head and tail...
            [ head; tail ] |> Block.fromRows |> Block.print)


        // done todo plotTics
        let ticLine = Segment.empty w
        let numberLine = Segment.empty w
        let mutable current = t.Hour

        [ for pos in 0..w ->
              let timeSlot = t.AddMinutes((float pos) * slotMins - offsetMins)
              let hour = timeSlot.Hour

              if hour % 3 = 0 && hour <> current then
                  current <- hour
                  Some(pos, timeSlot)
              else
                  None ]
        |> List.filter(fun opt -> Option.isSome opt)
        |> List.map(fun opt -> Option.get opt)
        |> List.iter (fun (pos, dt) ->
            let tic = Cell.make (char Literal.caret) (style Normal)
            let hourStr = if c.Hours12 then dt.ToString("ht") else dt.ToString("%H")

            let hourCell =
                Segment.fromString hourStr
                |> Segment.withStyle(style Normal)

            ticLine[pos] <- tic
            Segment.insertCellsInPlace pos hourCell numberLine)

        // note wanted to do tics more 'cleverly' but couldn't think of an improvement

        if c.Tics then
            [ ticLine; Segment.empty w; numberLine ]
            |> Block.fromRows
            |> Block.print
        else
            ()

    // todo complete
    let handleLivePlot = ()

module Args =

    open System

    open Configuration

    open Argu

    // todo add flag save
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

    let parser = ArgumentParser.Create<FitzArguments>(programName = "fitz")

    // idea use active pattern
    // todo complete
    let checkTimezoneLocation (tz : string) : bool = false

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
    // note do not reinvent the wheel
    // idea return Result
    let parseTime (s : string) : DateTime =
        match DateTime.TryParse(s) with
        | true, v -> v
        | false, _ -> failwith $"invalid time: {s}"

    // done todo complete
    // // idea appSettings/runConfig/runSettings
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
