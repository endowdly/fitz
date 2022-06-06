namespace fitz

open System 

module Plot =

    open Configuration
    open Helper

    let SymbolRectanglesDay = '█'
    let SymbolRectanglesTwilight = '▒'
    let SymbolRectanglesNight = ' '
    let SymbolMono = '#'
    let SymbolBork = '!' 
    let CharBar = '|'
    let CharCaret = '^'
    let SymbolSunMoonTwilight = '☼'
    let SymbolSunMoonDay = '☀' 
    let SymbolSunMoonNight = '☾' 

    let configLocationToLocation x : Location = { Name = x.Name; TimeZone = tryGetTimeZone x.TimeZone } 

    let (|Hour|NotHour|) n = 
        if 0 <= n && n < 24 then Hour n else NotHour

    let getContext cfg n =
        let x = cfg.Style.DaySegmentation
        match n with
        | Hour n -> 
            match n with 
            | _ when x.HourNight <= n || n < x.HourMorning -> Night
            | _ when n < x.HourDay -> Morning
            | _ when n < x.HourEvening -> Day
            | _ when n < x.HourNight -> Evening
            | _ -> Normal
        | NotHour -> Normal

    // combined the contexts and symbol maps into this function
    let getHourSymbol cfg n = 
        let x = getContext cfg n

        match cfg.Style.Symbols with
        | Rectangles -> 
            match x with
            | Morning
            | Evening -> SymbolRectanglesTwilight
            | Day -> SymbolRectanglesDay
            | Night -> SymbolRectanglesNight
            | Normal -> SymbolBork 
        | SunMoon -> 
            match x with
            | Morning
            | Evening -> SymbolSunMoonTwilight
            | Day -> SymbolSunMoonDay
            | Night -> SymbolSunMoonNight
            | Normal -> SymbolBork 
        | Mono -> SymbolMono 

        
    let getStyle cfg n =
        let s0 = Style.empty 
        match getContext cfg n with 
        | Morning -> s0 |> Style.withForeground (tryGetColor cfg.Style.Colors.ColorMorning)
        | Day -> s0|> Style.withForeground (tryGetColor cfg.Style.Colors.ColorDay)
        | Evening -> s0|> Style.withForeground (tryGetColor cfg.Style.Colors.ColorEvening)
        | Night -> s0|> Style.withForeground (tryGetColor cfg.Style.Colors.ColorNight)
        | _ -> s0
    
    let normalStyle cfg =
        let s0 = Style.empty 
        let fg, bg = cfg.Style.Colors.Foreground, cfg.Style.Colors.Background 
        match fg, bg with
        | "", "" -> s0
        | fg, "" -> s0 |> Style.withForeground (tryGetColor fg)
        | "", bg -> s0 |> Style.withBackground (tryGetColor bg)
        | fg, bg ->
            s0
            |> Style.withForeground(tryGetColor fg)
            |> Style.withBackground(tryGetColor bg) 

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
    let getPlot (cfg : Config) (t : DateTime) = 
        let utc = t.ToUniversalTime() 
        let styleNormal = normalStyle cfg
        let style = 
            match cfg.Style.Colorize with
            | false -> (fun _ -> styleNormal)
            | true -> getStyle cfg

        let w =
            if cfg.Stretch then
                Console.WindowWidth
            else 
                Console.WindowWidth * 13 / 21 // approx golden ratio

        let nowSlot = w / 2 
        let minPerSlot = 24 * 60 / w
        let minOffset = minPerSlot * w / 2
        let timeSlot = [| for t in 0 .. w -> utc.AddMinutes(float <| t * minPerSlot - minOffset) |]

        // note determining this programmatically can take a bit of time
        // this has to generate and check every tick in a second -- we can speed up execution
        // by shrinking the check range or eliminate it by passing a boolean like: plotTime c t b
        // just have Args.parseFlags emit a bool at the end to pass to here
        // the longest time will be when an arbitrary time is passed
        // Seq.contains (lazy) bails if it finds a generated match so 'now' execution will be fast
        let markerStr =
            let plusOne = t.AddSeconds(1.0)
            let inTime = times t plusOne |> Seq.contains DateTime.Now
            if inTime then "now" else "time"

        let markerStrSeg = Segment.fromString markerStr
        let markerTimeSeg = Segment.fromString(formatTime cfg.Hours12 t)

        let markerSeg =
            Segment.centered "v" w
            |> Segment.insertCells (nowSlot - markerStr.Length - 1) markerStrSeg
            |> Segment.insertCells (nowSlot + 2) markerTimeSeg
            |> Segment.withStyle styleNormal

        let timezones : Location list =
            cfg.TimeZones
            |> Array.map configLocationToLocation
            |> List.ofArray
            |> (fun ls -> { Name = "Local"; TimeZone = TimeZoneInfo.Local } :: ls)

        let descriptionLength =
            timezones
            |> List.map(fun loc -> loc.Name)
            |> List.maxBy(fun s -> String.length s)
            |> String.length

        let descriptionLengthPlus2 = descriptionLength + 2 

        let timebars =
            timezones
            |> List.map (fun tz ->
                let t = formatDateTime cfg.Hours12 (toTimeZone utc tz.TimeZone)
                let sDesc = (tz.Name + ":").PadRight(descriptionLengthPlus2) + t
                let cDesc = String.length sDesc
                let xDesc = Segment.fromString sDesc
                let segCell = Cell.make CharBar Style.empty

                let desc =
                    let seg0 =
                        Segment.empty w
                        |> Segment.insertCells 0 xDesc
                        |> Segment.withStyle styleNormal

                    if (cDesc - 1) < nowSlot then 
                        seg0[nowSlot] <- segCell
                    
                    seg0 

                let bar =
                    timeSlot
                    |> Array.map (fun t ->
                        let hr = (toTimeZone t tz.TimeZone).Hour
                        let char = getHourSymbol cfg hr
                        let style = style hr
                        Cell.make char style)

                bar[nowSlot] <- segCell
                // bar[nowSlot] <- { bar[nowSlot] with Char = CharBar }

                [ desc; bar ])

        let ticLine = Segment.empty (Array.length timeSlot)
        let numberLine = Segment.empty (Array.length timeSlot)
        let mutable curr = t.Hour 

        timeSlot
        |> Array.mapi (fun pos dt -> 
                let t = toTimeZone dt TimeZoneInfo.Local
                let hour = t.Hour

                if hour % 3 = 0 && hour <> curr then
                    curr <- hour
                    Some (pos, t)
                else
                    None)
        |> Array.filter (fun opt -> Option.isSome opt)
        |> Array.map (fun opt -> Option.get opt)
        |> Array.iter (fun (pos, dt) ->
            let tic = Cell.make CharCaret styleNormal
            let hourStr = if cfg.Hours12 then dt.ToString("ht") else dt.ToString("%H")

            let hourCell =
                Segment.fromString hourStr
                |> Segment.withStyle styleNormal

            ticLine[pos] <- tic
            Segment.insertCellsInPlace pos hourCell numberLine) 

        // note wanted to do tics more 'cleverly' but couldn't think of an improvement

        let rows = markerSeg :: List.concat timebars

        if cfg.Tics then
            [ ticLine
              numberLine ]
            |> List.append rows
            |> Block.fromRows
        else
            rows |> Block.fromRows

    let plot = Block.print