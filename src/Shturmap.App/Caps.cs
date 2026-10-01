using System.Globalization;

namespace Shturmap.App;

/// <summary>Uppercase for labels in the game's style (x:Bind can call static functions from any template).</summary>
public static class Caps
{
    public static string Of(string? text) => (text ?? "").ToUpper(CultureInfo.CurrentCulture);
}

/// <summary>One line of the keyboard help: the key as a keycap, and what it does.</summary>
public sealed record KeyHelp(string Key, string Action);
