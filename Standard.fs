namespace fitz

open System
open Helper

module Style =

    // Note SGR: select graphic rendition parameters
    // Link -> https://en.wikipedia.org/wiki/ANSI_escape_code#SGR_(Select_Graphic_Rendition)_parameters

    [<Literal>]
    let AnsiCommand = "\u001b["

    [<Literal>]
    let AnsiReset = "\u001b[0m"

    /// An alias for a string
    type SgrString = string

    /// An alias for a string
    type AnsiCommandString = string

    /// A simple type to distinguish SGR layers
    type ColorLayer =
        | Foreground
        | Background

    let empty = { Foreground = None; Background = None; Attributes = [] }
    let withForeground color style = { style with Foreground = color }
    let withBackground color style = { style with Background = color }
    let normal style = { style with Attributes = [] }
    let addAttribute attr style = { style with Attributes = attr :: style.Attributes }

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
        let fg =
            match style.Foreground with
            | Some color -> fgSgrFromColor color
            | None -> "39"

        let bg =
            match style.Background with
            | Some color -> bgSgrFromColor color
            | None -> "49"

        let attrs =
            style.Attributes
            |> List.map sgrFromAttr
            |> String.concat ";"

        [ fg; bg; attrs ]

    let csFromSgr ls : AnsiCommandString = AnsiCommand + (String.concat ";" ls).TrimEnd(';') + "m"
    let csFromStyle = sgrFromStyle >> csFromSgr
    let reset = printf $"{AnsiReset}"

module Cell =
    let make char style = { Char = char; Style = style }
    let empty = make ' ' Style.empty
    let withStyle style cell = { cell with Style = style }
    let print cell = printf $"{Style.csFromStyle cell.Style}{cell.Char}{Style.AnsiReset}"

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
        let pos = (n / 2) - ((String.length str) / 2)

        insertCells pos insert into

module Block =
    let make m n = Array2D.create m n Cell.empty
    let withStyle style block = Array2D.map (fun cell -> Cell.withStyle style cell) block

    let print block =
        [ 0 .. (Array2D.length1 block) - 1 ]
        |> List.map(fun row -> block[row, *])
        |> List.iter(fun row -> Segment.print row)

    let fromRows (ls : Cell [] list) =
        let m = List.length ls
        let n = List.maxBy (fun row -> Array.length row) ls |> Array.length
        let block = make m n

        [ 0 .. m - 1 ]
        |> List.iter(fun row -> block[row, *] <- Segment.insertCells 0 ls[row] block[row, *])

        block
