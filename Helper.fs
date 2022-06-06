namespace fitz

open System
open System.Drawing

module Helper =

    let rec times (from : DateTime) (toTime : DateTime) =
        seq {
            if from <= toTime then
                yield from
                yield! times (from.AddTicks(1)) toTime
        }

    // These functions will let us turn our json unfriedly types into core types
    // idea maybe bind to a Result for parseTimeZone
    // todo -> Result
    let tryGetTimeZone s =
        try
            TimeZoneInfo.FindSystemTimeZoneById(s)
        with
        | _ ->
            eprintfn $"Could not find timezone '{s}, defaulting to system timezone"
            TimeZone.Local 

    // todo -> Result
    let tryGetColor =
        function
        | "" -> None
        | s ->
            try
                Some <| ColorTranslator.FromHtml(s)
            with
            | _ ->
                eprintfn $"{s} is not a known color or html hex color, using default color"
                None

    let formatTime b (t : DateTime) =
        // standard formatters are locale specific so we must use custom formatters to enforce a style
        // link https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings

        if b then t.ToString("h:mmtt") else t.ToString("HH:mm")

    let formatDay (t : DateTime) = t.ToString("ddd dd MMM yyyy") 
    let formatDateTime b (t : DateTime) = (formatDay t) + " " + (formatTime b t) 

    // todo -> Result
    let toTimeZone t tz =
        try
            TimeZoneInfo.ConvertTimeFromUtc(t, tz)
        with
        | _ ->
            eprintfn $"Could not convert to {tz.ToString()} using local"
            t

module ConsoleHelper =
    let useAlternateScreenBuffer = printf "\u001b[?1049h"
    let useMainScreenBuffer = printf "\u001b[?1049l"

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
