namespace fitz

[<AutoOpen>]
module GroundTypes =

    /// An alias for TimeZoneInfo
    type TimeZone = System.TimeZoneInfo

    /// An alias for Color
    type Color = System.Drawing.Color

    /// A labeled TimeZone
    type Location = { Name : string; TimeZone : TimeZone }

    /// Describes what type of plot kind we should use
    type Context =
        | Normal
        | Morning
        | Day
        | Evening
        | Night

    /// Describes the SGR options
    type Attribute =
        | Bold
        | Blink
        | Reverse
        | Underline
        | Dim
        | Italic
        | Strikethrough

    /// A record to hold all the SGR attributes
    type Style =
        { Foreground : Color option
          Background : Color option
          Attributes : Attribute list }

    /// Represents a character with a style
    type Cell = { Char : char; Style : Style }
