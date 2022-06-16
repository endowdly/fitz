# fitz
CLI TimeZone Info for .NET 6 in FSharp. Ported from https://github.com/merschformann/gotz

## Installation

### Scoop

[_NotImplementedException_](https://docs.microsoft.com/en-us/dotnet/api/system.notimplementedexception?view=net-6.0)

### Directly via [dotnet](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools)
 
[_NotImplementedException_](https://docs.microsoft.com/en-us/dotnet/api/system.notimplementedexception?view=net-6.0)

### Binary 

[_NotImplementedException_](https://docs.microsoft.com/en-us/dotnet/api/system.notimplementedexception?view=net-6.0)

## Usage 

`fitz` has largely identical appearance and options to `gotz` is implemented completely differenrlt with some functional differences.

### Summary of differences

What `fitz` does differently than `gotz`:

- `fitz` parses time differently
- `fitz` parses timezones differently and can use both IANA and Microsoft Timezone Ids
- `fitz` uses an alternate screen buffer when entering live mode to keep your current buffer intact
- `fitz` does not overwrite your configuration file with current command-line settings
- `fitz` has a _slightly_ different configuration file

### Time

Can show the current time:

```powershell
fitz
```

or can show any time...
```powershell
fitz "23:00"
```

```powershell
fitz "03/17/2007 18:30"
``` 

[These are the strings](https://docs.microsoft.com/en-us/dotnet/api/system.datetime.parse?view=net-6.0#StringToParse) that can be parsed by .NET's library to see what `fitz` can handle.

### Timezones

Like `gotz`, `fitz` uses [IANA timezones](https://en.wikipedia.org/wiki/List_of_tz_database_time_zones).
Unlike `gotz`, `fitz` also accepts and understands [Microsoft TimeZone Ids](https://docs.microsoft.com/en-us/power-apps/developer/data-platform/time-zone-entities).

Get specific timezones:

```powershell
fitz --timezones "Work:America/Chicago,Home:America/New_York"
```

```powershell
fitz --timezones "Home:Eastern Standard Time,Dream Home:Hawaiian Standard Time"
```

`fitz` handles timezones on the command-line almost exactly like `gotz` with the following syntax: `[label:]tz, ...`  
Whitespace _is ignored_.

### Configuration

To force your command-line settings to save to your configuration file:

```powershell
fitz --tics --hours12 --save
```



### Full Usage

See [gotz](https://github.com/merschformann/gotz) or

```powershell
fitz --help
``` 

`fitz` remains largley identical to `gotz`.

## Build (and run) from Source

### Requirements

- dotnet CLI
- .NET SDK 6
- .NET Runtime 6

The latest versions of .NET can be installed from [here](https://aka.ms/dotnet-core-download) and various package managers.

### Steps

1. Enter the project directory
2. `dotnet build`
3. `dotnet run`

Pass command line arguments to `fitz` directly after the call to `run`, e.g. `dotnet run --tics --live`

## Configuration file

`fitz` has a very similiar configuration layout to `gotz`.
However, `fitz` uses the `FSharp.Json` library, which allows more complicated type serialization than the standard .NET json serializer and the standard `go` json marshalling class.
Because of this flexibility, configuration can be simpler.

`gotz` uses the go `tcell` package to handle live terminal updates.
`fitz` implements a small terminal cell module with its own SGR style library.
This simplifies the colorization configuration -- both the live and static views use the same colors.

```json 
{
   // Tracks the version of the configuration file (automatic)
  "config_version": "1.0.0",
  // Configures the timezones to be shown (can be IANA or Microsoft)
  "timezones": [
    // Timezones have a Label (Name) and Timezone string that evaluates to a timezone id
    {
      "name": "West Coast",
      "timezone": "America/Los_Angeles"
    },
    {
      "name": "Berlin",
      "timezone": "Europe/Berlin"
    },
    {
      "name": "New Zealand",
      "timezone": "Pacific/Auckland"
    }
  ],
  // Configures the style of the plot
  "style": {
    // Select symbose to use for time blocks ('Mono'|'Rectangles'|'SunMoon')
    "symbols": "Rectangles",
    // Use coloring or not
    "colorize": true,
    // Choose which hours to segment the time bars
    "day_segments": {
       // Hour of the morning to start (0-23)
      "morning": 6,
       // Hour of the day (business time) to start (0-23)
      "day": 8,
       // Hour of the evening to start (0-23)
      "evening": 18,
       // Hour of the night to start (0-23)
      "night": 22
    },
    // Defines the colors for the segments
    // Colors can be web-safe color names like "cyan" or "aqua"
    // Colors can be hex codes like #00ff00 or #dc143c
    "colors": {
      // Color of the morning segment
      "color_morning": "darkcyan", 
      // Color of the day segment
      "color_day": "cyan",
      // Color of the evening segment
      "color_evening": "darkcyan",
      // Color of the night segment
      "color_night": "blue",
      // Foreground color that overrides the default console color (optional)
      "foreground": "",
      // Background color that overrides the default console color (optional)
      "background": ""
    }
  },
  // Plot tics on the bottom line for the local time
  "tics": false,
  // Stretch across the full terminal width
  "stretch": false,
  // Use a 12-hour format
  "hours12": false,
  // Plot bars continuously
  "live": false
}
```

## FAQ 

1. Is this any good?

> Yes

2. Why is it good? 

> Because gotz is good.

3. Why did you port this? 

> Mostly practice. I wanted to see if I could get gotz to work seemlessly with f-sharp.






