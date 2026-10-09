namespace Shturmap.Core;

/// <summary>
/// The game's language, as its settings name it, in the codes everyone else uses (ISO 639-1). The game names some
/// languages its own way ("ge" for German); tarkov.dev's files (<c>maps_de</c>) and Windows' text recognition
/// (<c>de-DE</c>) both go by the common code. One table for both (review of 2026-10-09: the data loader and the extract
/// list's reader each had a copy).
/// </summary>
public static class GameLanguage
{
    /// <summary>The common code for the game's own ("ge" → "de"); a code the game shares with everyone stays as it is,
    /// in lower case. Null when the game names no language.</summary>
    public static string? Common(string? gameLanguage) => gameLanguage?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "ge" => "de",
        "cz" => "cs",
        "jp" => "ja",
        "kr" => "ko",
        "po" => "pt",
        "tu" => "tr",
        "ch" => "zh",
        "es-mx" => "es",
        var code => code,
    };
}
