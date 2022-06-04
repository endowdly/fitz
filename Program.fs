namespace fitz

(*
module Native =
    open System
    open System.Runtime.InteropServices

    type HANDLE = IntPtr
    type DWORD = uint32

    let ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0200u
    let STD_INPUT_HANDLE = uint32 -10

    [<DllImport("kernel32.dll")>]
    extern HANDLE GetStdHandle(DWORD nStdHandle)

    [<DllImport("kernel32.dll")>]
    extern bool GetConsoleMode(HANDLE hConsoleHandle, DWORD& dwMode)

    [<DllImport("kernel32.dll")>]
    extern bool SetConsoleMode(HANDLE hConsoleHandle, DWORD dwMode)

module ConsoleHelper =

    open Native

    let enableVT =
        let getStdHandle = GetStdHandle(STD_INPUT_HANDLE)
        let mutable dwMode = 0u

        match GetConsoleMode(getStdHandle, &dwMode) with
        | false -> printfn "could not retrieve console mode"
        | true ->
            dwMode <- dwMode ||| ENABLE_VIRTUAL_TERMINAL_PROCESSING

            match SetConsoleMode(getStdHandle, dwMode) with
            | false -> printfn "could not set console mode"
            | true -> ()

    let useAlternateScreenBuffer = printf "\u001b[?1049h"
    let useMainScreenBuffer = printf "\u001b[?1049l"

    *)

module Fitz =

    [<EntryPointAttribute>]
    let main argv =

        // get configuration
        let config, err = Configuration.load
        // if err block...

        // parse flags
        let settings, time = Args.parseFlags config argv

        // update config
        // Um. Don't hammer user configs.
        // Maybe add a flag or something, but this should not be default behavior.
        match Args.canOverwriteConfig argv with
        | false -> ()
        | true -> Configuration.save config

        // plot time
        // add a live check here
        // if live then useAlternateScreenBuffer
        // maybe seperate plot to plotLive
        // todo console window adjust event
        if settings.Live then
            // turn off the cursor
            System.Console.CursorVisible <- false

            // time will always be now
            let s, r = Plot.plotLive settings

            // start the timers
            s.Enabled <- true

            // set up the app live
            let mutable quit = false 
            let stop (s : System.Timers.Timer) (r : System.Timers.Timer) =
                s.Enabled <- false
                r.Enabled <- false
                s.Dispose()
                r.Dispose()
                quit <- true

            System.Console.CancelKeyPress.Add(fun _ -> stop s r)

            while not quit do
                let cki = System.Console.ReadKey(true)
                if cki.Key = System.ConsoleKey.Q || cki.Key = System.ConsoleKey.Escape then stop s r

            // turn the cursor back on
            System.Console.CursorVisible <- true
        else
            // drop the block and print and leave
            Plot.getPlot settings time |> Plot.plot

        // if live then useMainScreenBuffer
        0
