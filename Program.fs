namespace fitz 

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

module Fitz = 

    [<EntryPointAttribute>]
    let main argv = 

        // get configuration 
        // todo use pattern matching 
        let config, err = Configuration.load 
        // if err block...

        // parse flags 
        let results = Args.parser.ParseCommandLine argv
        let settings, time, changed, err = Args.parseFlags config Version

        // update config
        // Um. No. We don't hammer user configs.
        // Maybe add a flag or something, but this should not be default behavior.
        // SKIP

        // plot time
        // maybe add a live check here
        // if live then useAlternateScreenBuffer
        // maybe seperate plot to plotLive 
        Plot.plot config time 

        // if live then useMainScreenBuffer
        0


        