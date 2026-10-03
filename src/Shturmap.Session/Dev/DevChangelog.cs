#if DEVTOOLS
using System.Globalization;
using System.Text.Json;

namespace Shturmap.Session.Dev;

/// <summary>One commit in a developer build's changelog.</summary>
public sealed record DevCommit(string Hash, DateTimeOffset? Date, string Subject);

/// <summary>
/// What a developer build holds (owner, 2026-10-03: "add a changelog of what was recently changed so a) I can check if
/// a change is already live and b) what was recently changed"): <c>dev\changelog.json</c> beside the app, written by
/// <c>eng\dev.ps1</c> — the build's commit and time, and its commits since the last release (or the last 50), newest
/// first. Read defensively: a missing or broken file is "no changelog", never an error.
/// </summary>
public sealed record DevChangelog(string? Build, DateTimeOffset? Built, IReadOnlyList<DevCommit> Commits)
{
    public const string RelativePath = "dev\\changelog.json";

    public static DevChangelog? Read(string appFolder)
    {
        var path = Path.Combine(appFolder, "dev", "changelog.json");
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The changelog in <paramref name="json"/>, or null when it isn't one. Commits without a hash or a subject
    /// are left out; they stay newest first.</summary>
    public static DevChangelog? Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            var commits = new List<DevCommit>();
            if (root.TryGetProperty("commits", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in list.EnumerateArray())
                {
                    if (c.ValueKind != JsonValueKind.Object || Text(c, "hash") is not { Length: > 0 } hash || Text(c, "subject") is not { Length: > 0 } subject)
                        continue;
                    commits.Add(new DevCommit(hash, Date(Text(c, "date")), subject));
                }
            }
            return new DevChangelog(Text(root, "build"), Date(Text(root, "built")), commits);
        }
    }

    /// <summary>The commits whose hash or subject holds every word of <paramref name="filter"/> (any case).</summary>
    public IReadOnlyList<DevCommit> Matching(string? filter)
    {
        var words = (filter ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0
            ? Commits
            : Commits.Where(c => words.All(w => c.Subject.Contains(w, StringComparison.OrdinalIgnoreCase) || c.Hash.StartsWith(w, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    /// <summary>The commit an assembly was built from, from its informational version ("0.2.0+b018f54…"), shortened.</summary>
    public static string? CommitOf(string? informationalVersion) =>
        informationalVersion?.Split('+', 2) is [_, var hash] && hash.Length >= 7 ? hash[..7] : null;

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTimeOffset? Date(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d) ? d : null;
}
#endif
