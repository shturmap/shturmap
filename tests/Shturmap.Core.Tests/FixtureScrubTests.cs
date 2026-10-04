using System.Text.RegularExpressions;

namespace Shturmap.Core.Tests;

/// <summary>
/// The log fixtures come from real game logs, and the repository is public: nothing in them may identify a player.
/// Every id is either public game data (a quest, a trader, an item template) or a placeholder written by
/// tools\make-log-fixtures.ps1; tokens, session ids and addresses are masked. See CLAUDE.md, "Players' data", and
/// docs/DESIGN.md §3, "Test fixtures". A failure names the file, the line and what stands before the value, never
/// the value itself: test output is public too.
/// </summary>
public partial class FixtureScrubTests
{
    // tarkov.dev's trader ids, the only ids a message's dialogId and uid may keep (anything else there is a player).
    private static readonly HashSet<string> Traders =
    [
        "54cb50c76803fa8b248b4571", "54cb57776803fa99248b456e", "579dc571d53a0658a154fbec", "58330581ace78e27b8b10cee",
        "5935c25fb3acc3127c3d8cd9", "5a7c2eca46aef81a7ca2145d", "5ac3b934156ae10c4430e83c", "5c0647fdd443bc2504c2d371",
        "638f541a29ffd1183d187f57", "656f0f98d80a697f855d34b1", "6617beeaa9cfa777ca915b7c",
    ];

    [Fact]
    public void Log_fixtures_hold_no_id_that_is_neither_public_nor_a_placeholder()
    {
        var violations = Lines().SelectMany(l => HexRun().Matches(l.Text)
            .Where(m => !Allowed(l.Text[..m.Index], m.Value.ToLowerInvariant()))
            .Select(m => $"{l.File}:{l.Number} has an id after \"{Tail(l.Text[..m.Index])}\"")).ToList();
        Assert.True(violations.Count == 0, Report(violations));
    }

    [Fact]
    public void Log_fixtures_hold_no_token_session_id_account_or_address()
    {
        var violations = Lines().SelectMany(l => new[]
        {
            Secret().IsMatch(l.Text) ? "a token or session id that isn't \"redacted\"" : null,
            Account().IsMatch(l.Text) ? "an account id" : null,
            Address().Matches(l.Text).Any(m => m.Value != "0.0.0.0") ? "a network address" : null,
        }.OfType<string>().Select(what => $"{l.File}:{l.Number} has {what}")).ToList();
        Assert.True(violations.Count == 0, Report(violations));
    }

    private static bool Allowed(string before, string id) =>
        Placeholder().IsMatch(id)
        || ItemTemplate().IsMatch(before)
        || InsideTemplateId().IsMatch(before)
        || (SenderId().IsMatch(before) && Traders.Contains(id));

    private static IEnumerable<(string File, int Number, string Text)> Lines()
    {
        var files = Directory.EnumerateFiles(Fixtures.PathTo("logs"), "*.log", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(files);
        return files.SelectMany(f => File.ReadLines(f).Select((text, i) => (Path.GetFileName(f), i + 1, text)));
    }

    private static string Tail(string text) => text.Length <= 28 ? text.TrimStart() : text[^28..].TrimStart();

    private static string Report(List<string> violations) =>
        string.Join(Environment.NewLine, violations.Take(20).Append(violations.Count > 20 ? $"… and {violations.Count - 20} more" : ""));

    [GeneratedRegex(@"\b[0-9a-f]{24,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex HexRun();

    // Profiles are 000…001, 000…002; every other masked id is ffffffff and a number; a masked push channel is all zeros.
    [GeneratedRegex(@"^(0{16}\d{8}|f{8}\d{16}|0{25,})$")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"""_tpl""\s*:\s*""$")]
    private static partial Regex ItemTemplate();

    // "templateId": "<quest> successMessageText <trader> 0": quest and trader ids, and words.
    [GeneratedRegex(@"""templateId""\s*:\s*""[^""]*$")]
    private static partial Regex InsideTemplateId();

    [GeneratedRegex(@"""(dialogId|uid)""\s*:\s*""$")]
    private static partial Regex SenderId();

    [GeneratedRegex(@"""(\w*token\w*|aid|accountId|session\w*|sid)""\s*:\s*""(?!redacted"")", RegexOptions.IgnoreCase)]
    private static partial Regex Secret();

    [GeneratedRegex(@"AccountId:\s*(?!0\b)\d+", RegexOptions.IgnoreCase)]
    private static partial Regex Account();

    // Four numbers, not part of a longer dotted run (the game's version has five).
    [GeneratedRegex(@"(?<![\d.])\d{1,3}(\.\d{1,3}){3}(?![\d.])")]
    private static partial Regex Address();
}
