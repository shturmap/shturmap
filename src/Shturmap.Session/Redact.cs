using System.Text.RegularExpressions;

namespace Shturmap.Session;

/// <summary>
/// What every text that may leave the PC goes through (diagnostics, reports, crash reports): the profile folder
/// written as %USERPROFILE%, and every 24-digit id (the game's profile and account ids, tarkov.dev's quest ids) cut to
/// <c>&lt;id&gt;</c> (owner, 2026-10-03; docs/DESIGN.md §8, "Diagnostics", "Reports").
/// </summary>
public static partial class Redact
{
    public static string Text(string text, string? profile) => Ids().Replace(LogFile.Mask(text, profile), "<id>");

    /// <summary>The text redacted with this user's profile folder.</summary>
    public static string Text(string text) => Text(text, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    // Profile, account and quest ids are 24 hex digits in the game's logs and tarkov.dev's data.
    [GeneratedRegex(@"\b[0-9a-fA-F]{24}\b")]
    private static partial Regex Ids();
}
