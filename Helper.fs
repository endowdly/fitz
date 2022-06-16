namespace fitz

open System
open System.Drawing

module Helper =

    /// tickGen creates an infinite sequence of ticks from .. toTime
    let rec tickGen (from : DateTime) (toTime : DateTime) =
        seq {
            if from <= toTime then
                yield from
                yield! tickGen (from.AddTicks(1)) toTime
        }

    // These functions will change json configuration types into standard types

    /// Tries to find a timezone by string. On failure, returns the local timezone.
    let tryGetTimeZone s =
        try
            TimeZoneInfo.FindSystemTimeZoneById(s)
        with
        | _ ->
            eprintfn $"ERROR: Could not find timezone '{s} -- fallback to system timezone"
            TimeZone.Local

    /// Tries to find a color by string or hex code. On failure, returns None.
    let tryGetColor =
        function
        | "" -> None
        | s ->
            try
                Some <| ColorTranslator.FromHtml(s)
            with
            | _ ->
                eprintfn $"ERROR: {s} is not a known color or html hex color -- fallback to default color"
                None

    /// Formats time in a custom way.
    let formatTime b (t : DateTime) =
        // Standard formatters are locale specific so we must use custom formatters to enforce a style
        // link https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings

        if b then t.ToString("h:mmtt") else t.ToString("HH:mm")

    /// Formats date information in a custom way.
    let formatDay (t : DateTime) = t.ToString("ddd dd MMM yyyy")
    /// Returns a custom date and time string.
    let formatDateTime b (t : DateTime) = (formatDay t) + " " + (formatTime b t)

    /// Converts a universal time to a different timezone.
    let toTimeZone t tz =
        try
            TimeZoneInfo.ConvertTimeFromUtc(t, tz)
        with
        | _ ->
            eprintfn $"ERROR: Could not convert to {tz.ToString()} -- fallback to input"
            eprintfn "This error should never been seen! Please report -> https://github.com/endowdly/fitz/issues"
            t

module ConsoleHelper =
    let useAlternateScreenBuffer () = printf "\u001b[?1049h"
    let useMainScreenBuffer () = printf "\u001b[?1049l"

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
