namespace Shturmap.Session;

/// <summary>
/// "Follow my position", kept between runs (a setting in shturmap.db). Off unless the player turned it on, so the
/// map behaves as before until they do (owner, 2026-10-03; docs/DESIGN.md "Follow my position").
/// </summary>
public static class MapFollow
{
    public const string Setting = "followPosition";

    public static bool Parse(string? value) => value == "on";

    public static string Format(bool on) => on ? "on" : "off";
}
