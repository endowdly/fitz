namespace fitz

type Location = { Name : string; TZ : string }

type PlotColors =
    { StaticColorMorning : string
      StaticColorDay : string
      StaticColorEvening : string
      StaticColorNight : string
      StaticColorForeground : string
      DynamicColorMorning : string
      DynamicColorDay : string
      DynamicColorEvening : string
      DynamicColorNight : string
      DynamicColorForeground : string
      DynamicColorBackground : string }
