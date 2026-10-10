namespace Shturmap.App;

/// <summary>Uppercase for labels in the game's style (x:Bind can call static functions from any template), by the
/// language in use's rules and with ß as SS (<see cref="Shturmap.Core.UiLanguage.Upper"/>).</summary>
public static class Caps
{
    public static string Of(string? text) => Shturmap.Core.UiLanguage.Upper(text);
}

/// <summary>One line of the keyboard help: the key as a keycap, and what it does.</summary>
public sealed record KeyHelp(string Key, string Action);

/// <summary>One line of the map legend: the symbol as the map draws it, and what it means.</summary>
public sealed record MapLegendRow(Microsoft.UI.Xaml.Media.ImageSource Swatch, string Text);
