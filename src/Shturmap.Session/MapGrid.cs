namespace Shturmap.Session;

/// <summary>
/// "Grid on the map", kept between runs (a setting in shturmap.db): the squad grid, on unless the player unticked it
/// (owner, 2026-10-26: one setting, on by default; docs/DESIGN.md §4, "Map drawing", *The grid*).
/// </summary>
public static class MapGrid
{
    public const string Setting = "map.grid";

    public static bool Parse(string? value) => value != "off";

    public static string Format(bool on) => on ? "on" : "off";
}
