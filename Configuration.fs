namespace fitz

open System
open System.IO
open System.Text

module Configuration =

    open FSharp.Json

    [<Literal>]
    let ConfigVersion = "1.0.0"

    /// Module-wide encoding
    let Enc = Encoding.UTF8

    /// Represents the configurable symbols in the plotted time bars.
    type Symbols =
        | [<JsonField("rectangles")>] Rectangles
        | [<JsonField("mono")>] Mono
        | [<JsonField("sun-moon")>] SunMoon

    /// A structure containing the plot colors.
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

    /// A structure that contains a tagged TimeZone that can be easily mapped to a Location.
    [<Struct>]
    type ConfigLocation =
        { [<JsonField("name")>]
          Name : string
          [<JsonField("timezone")>]
          TimeZone : string }

    /// A structure to configure how the day is broken up by hours.
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

    let private (|Json|JsonFail|) data =
        try
            Json(Json.serialize data)
        with
        | ex -> JsonFail ex

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

    let saveDefault () = save defaultConfig

    // todo better error handling
    // idea return result<config, ex>
    let load =

        // If no configuration file exists, create one
        if not (FileInfo defaultConfigFile).Exists then
            saveDefault()

        // Read configuration file
        match defaultConfigFile with
        | Contents v ->

            // Deserialize (unmarshal)
            match v with
            | Config data ->
                // Check version and validate in place
                if data.ConfigVersion = ConfigVersion then
                    data, false
                else
                    defaultConfig, true

            | ConfigFail _ ->
                eprintfn "Could not deserialize config, using default"
                defaultConfig, true

        | AccessDenied ->
            eprintfn "Could not load config file, using default"
            defaultConfig, true
