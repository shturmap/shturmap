using SkiaSharp;

namespace Shturmap.Map;

/// <summary>
/// The design system's colours (docs/DESIGN.md §4, "Design system"): one table for the app, the map, the brand files
/// and the website. App.xaml repeats the app's as Color resources (XAML can't read these), the website's
/// assets/site.css as CSS variables and brand\build.cs as its own constants; DesignTokenTests keep every copy equal
/// to this table. A colour that isn't here doesn't belong in Shturmap's surfaces.
/// </summary>
public static class Palette
{
    // Surfaces, darkest first, and the two hairlines.
    public const string Ground = "#0B0C0B";
    public const string Rail = "#101110";
    public const string Panel = "#151614";
    public const string Raised = "#1E1F1B";
    public const string Line = "#2A2B27";
    public const string LineStrong = "#45463F";
    /// <summary>An inventory cell behind item icons.</summary>
    public const string Cell = "#1A1B18";

    // Text.
    public const string Ink = "#D9D5C4";
    public const string Muted = "#8A8778";

    // The one accent: quests, objectives, distances, "on". Its lighter steps for hover and focus, its darker one for
    // pressed (Windows' accent ramp in App.xaml).
    public const string Amber = "#C9AD62";
    public const string AmberHover = "#D6BE7E";
    public const string AmberHi = "#E2CF9C";
    public const string AmberDeep = "#A88F4E";

    // State and kind, each with one meaning (Visual language): extracts by side, transits, bosses, the player, the kept
    // or picked quests.
    public const string Green = "#8DA65E";
    public const string Teal = "#6F9A94";
    public const string Khaki = "#B7B77A";
    public const string Violet = "#9C8CC4";
    public const string Red = "#B8604A";
    public const string Sand = "#E9E2C8";
    // The dev build's icon plate; until 2026-10-04 also the one colour of every picked quest.
    public const string Kept = "#3FD2E0";
    // The picked quests, one colour each: the first eight of ColorBrewer's "Set3", a table of soft colours made for
    // telling categories apart on maps (owner, 2026-10-04, from a panel of six palettes: "Go for D"). In the order
    // picks take them: the first four are the ones easiest to tell from each other and from the map's own colours;
    // the orange, close to the quests' gold and to the lime for red-green colour-blind eyes, comes last.
    public const string Pick1 = "#8DD3C7";
    public const string Pick2 = "#FB8072";
    public const string Pick3 = "#80B1D3";
    public const string Pick4 = "#B3DE69";
    public const string Pick5 = "#FFFFB3";
    public const string Pick6 = "#FCCDE5";
    public const string Pick7 = "#BEBADA";
    public const string Pick8 = "#FDB462";

    // The logo on light backgrounds (brand files, README in light mode).
    public const string LightGround = "#F1F0EC";
    public const string LightInk = "#1E1F1B";
    public const string LightAmber = "#8C7436";

    // The logo's own drawing only (brand\build.cs): the plate's corner marks, the social preview's grid, and the dev
    // icon's cyan plate's edge and marks.
    public const string LogoMarks = "#4A4A41";
    public const string LogoGrid = "#171815";
    public const string DeveloperPlateEdge = "#1E6F78";
    public const string DeveloperPlateMarks = "#2A97A3";

    // The website only: a second text tone for long reading on the dark page, a dimmer one for what a file name holds
    // but Shturmap doesn't use, and a lighter teal that reads as text (the app's teal is a symbol colour).
    public const string WebInkSoft = "#BDB9A8";
    public const string WebDim = "#6E6C60";
    public const string WebTealText = "#8FB8B1";

    // The map's sheet for maps without artwork: two shades between the surfaces above, so its grid reads under markers.
    public const string SheetPanel = "#121311";
    public const string SheetMinor = "#1C1D1A";

    /// <summary>Every colour above by name, for the tests that keep the copies equal.</summary>
    public static IReadOnlyDictionary<string, string> All { get; } = typeof(Palette)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);

    public static SKColor Sk(string hex) => SKColor.Parse(hex);
}
