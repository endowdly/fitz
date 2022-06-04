namespace fitz

// todo add summary comments

[<AutoOpen>]
module Types = 

    type TimeZone = System.TimeZoneInfo

    type Color = System.Drawing.Color

    type Location = { Name : string; TimeZone : TimeZone }

    type Attribute =
        | Bold
        | Blink
        | Reverse
        | Underline
        | Dim
        | Italic
        | Strikethrough

    type Style = { Foreground : Color; Background : Color; Attributes : Attribute list } 
 
    type Cell = { Char : char; Style : Style } 
  
    type SgrString = string
  
    type AnsiCommandString = string
  
    type ColorLayer =
        | Foreground
        | Background

    type Context =
        | Normal
        | Morning
        | Day
        | Evening
        | Night
