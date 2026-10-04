using System.Text.Json;
using System.Text.RegularExpressions;

namespace Shturmap.Core.Logs;

/// <summary>
/// Recognises the handful of log entries Shturmap cares about. Everything is matched on substrings and every
/// unknown entry is ignored, because none of this is a documented format and patches do change it.
/// </summary>
public static partial class GameLogParser
{
    [GeneratedRegex(@"^Session mode:\s*(?<mode>\w+)", RegexOptions.CultureInvariant)]
    private static partial Regex SessionMode();

    [GeneratedRegex(@"^scene preset path:\s*(?<path>maps/[\w\-]+\.bundle)(?:.*?\brcid:(?<rcid>[\w\-]+))?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ScenePreset();

    [GeneratedRegex(@"\bLocation:\s*(?<loc>[^,'\s]+)", RegexOptions.CultureInvariant)]
    private static partial Regex Location();

    [GeneratedRegex(@"\bshortId:\s*(?<id>[A-Z0-9]{6})\b", RegexOptions.CultureInvariant)]
    private static partial Regex ShortId();

    [GeneratedRegex(@"^\[Transit\].*?\bRaidId:\s*(?<raid>[^,\s]+).*?\bLocations:\s*(?<loc>[\w\-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex Transit();

    [GeneratedRegex(@"\bProfileid:\s*(?<id>[0-9a-f]{24})\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ProfileId();

    public static GameEvent? Parse(LogRecord record)
    {
        var msg = record.Message;
        var at = record.Timestamp;

        if (record.Channel.Equals("push-notifications", StringComparison.OrdinalIgnoreCase))
            return ParseNotification(record);

        if (!record.Channel.Equals("application", StringComparison.OrdinalIgnoreCase))
            return null;

        if (SessionMode().Match(msg) is { Success: true } sm)
            return new SessionModeEvent(at, ModeFrom(sm.Groups["mode"].Value), sm.Groups["mode"].Value);

        if (ScenePreset().Match(msg) is { Success: true } sp)
            return new MapLoadingEvent(at, sp.Groups["path"].Value.ToLowerInvariant(), sp.Groups["rcid"].Success ? sp.Groups["rcid"].Value : null);

        if (msg.StartsWith("TRACE-NetworkGameCreate profileStatus", StringComparison.Ordinal))
        {
            var loc = Location().Match(msg);
            var id = ShortId().Match(msg);
            var profile = ProfileId().Match(msg);
            return new MatchSetupEvent(
                at,
                loc.Success ? loc.Groups["loc"].Value : null,
                id.Success ? id.Groups["id"].Value : null,
                profile.Success ? profile.Groups["id"].Value.ToLowerInvariant() : null);
        }

        if (Transit().Match(msg) is { Success: true } tr)
            return new TransitInfoEvent(at, tr.Groups["raid"].Value, tr.Groups["loc"].Value);

        if (msg.StartsWith("GameStarting:", StringComparison.Ordinal))
            return new GameStartingEvent(at);

        if (msg.StartsWith("GameStarted:", StringComparison.Ordinal))
            return new GameStartedEvent(at);

        if (StepOf(msg) is { } step)
            return new LoadingStepEvent(at, step);

        if (msg.StartsWith("PrepareSelectedProfileLocally", StringComparison.Ordinal))
        {
            var profile = ProfileId().Match(msg);
            return new ProfileLoadedEvent(at, profile.Success ? profile.Groups["id"].Value.ToLowerInvariant() : null);
        }

        if (msg.Contains("matching cancelled", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("matching aborted", StringComparison.OrdinalIgnoreCase))
            return new MatchingCancelledEvent(at);

        return null;
    }

    // "LocationLoaded:9.61 real:13.28 diff:3.67" and its kin; GameSpawn and GameSpawned come with GameRunned and add
    // nothing.
    private static LoadingStep? StepOf(string msg)
    {
        foreach (var (prefix, step) in Steps)
        {
            if (msg.StartsWith(prefix, StringComparison.Ordinal))
                return step;
        }
        return null;
    }

    private static readonly (string Prefix, LoadingStep Step)[] Steps =
    [
        ("MatchingCompleted:", LoadingStep.MatchingCompleted),
        ("LocationLoaded:", LoadingStep.LocationLoaded),
        ("GamePrepared:", LoadingStep.GamePrepared),
        ("GameCreated:", LoadingStep.GameCreated),
        ("PlayerSpawnEvent:", LoadingStep.PlayerSpawned),
        ("GamePooled:", LoadingStep.GamePooled),
        ("GameRunned:", LoadingStep.GameRunning),
    ];

    public static GameMode ModeFrom(string raw) => raw.ToLowerInvariant() switch
    {
        "pve" => GameMode.Pve,
        "regular" or "pvp" => GameMode.Pvp,
        var s when s.Contains("season", StringComparison.Ordinal) || s == "szn" => GameMode.Seasonal,
        _ => GameMode.Unknown,
    };

    private static GameEvent? ParseNotification(LogRecord record)
    {
        // Each notification is logged twice, as "Got notification | Kind" with its body and as a "Received
        // notification: Type: Kind" line; only the first counts. The group status kinds are read from that header
        // alone: their bodies are other players' profiles.
        if (record.Message.StartsWith("Got notification", StringComparison.Ordinal))
        {
            GroupStatus? group = record.Message.TrimEnd() switch
            {
                var m when m.EndsWith("GroupMatchRaidReady", StringComparison.Ordinal) => GroupStatus.Ready,
                var m when m.EndsWith("GroupMatchRaidNotReady", StringComparison.Ordinal) => GroupStatus.NotReady,
                var m when m.EndsWith("GroupMatchStartGame", StringComparison.Ordinal) => GroupStatus.Start,
                _ => null,
            };
            if (group is { } kind)
                return new GroupStatusEvent(record.Timestamp, kind);
        }

        if (record.Body is null)
            return null;

        if (record.Message.Contains("GroupMatchRaidSettings", StringComparison.Ordinal))
        {
            using var settings = TryParse(record.Body);
            if (settings is null || !Property(settings.RootElement, "raidSettings", out var raid) || String(raid, "location") is not { } location)
                return null;
            return new GroupRaidSettingsEvent(record.Timestamp, location, String(raid, "timeVariant"));
        }

        if (record.Message.Contains("UserConfirmed", StringComparison.Ordinal))
        {
            using var json = TryParse(record.Body);
            if (json is not { RootElement: { ValueKind: JsonValueKind.Object } root })
                return null;
            return new MatchSetupEvent(record.Timestamp, String(root, "location"), String(root, "shortId"), String(root, "profileid")?.ToLowerInvariant());
        }

        if (!record.Message.Contains("ChatMessageReceived", StringComparison.Ordinal))
            return null;

        using var doc = TryParse(record.Body);
        if (doc is null || !Property(doc.RootElement, "message", out var message) || message.ValueKind != JsonValueKind.Object)
            return null;

        var type = Property(message, "type", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetInt32(out var number) ? number : -1;
        // The insurer writes about a raid by its location: a trader message (2) when insured gear was lost, the
        // insurance return (8) with the gear hours later.
        if (type is 2 or 8 && Property(message, "systemData", out var system) && String(system, "location") is { } raidLocation)
            return new InsuranceNoticeEvent(record.Timestamp, type == 2 ? InsuranceNotice.Lost : InsuranceNotice.Returned, raidLocation, ItemsIn(message));
        QuestLogStatus? status = type switch
        {
            10 => QuestLogStatus.Started,
            11 => QuestLogStatus.Failed,
            12 => QuestLogStatus.Completed,
            _ => null,
        };
        var template = String(message, "templateId");
        if (status is null || template is null)
            return null;

        var questId = template.Split(' ', 2)[0];
        if (questId.Length != 24 || !questId.All(Uri.IsHexDigit))
            return null;

        var at = Property(message, "dt", out var dt) && UnixSeconds(dt) is { } seconds
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime
            : record.Timestamp;
        var eventId = String(doc.RootElement, "eventId") ?? String(message, "_id") ?? $"{questId}:{status}:{at:O}";
        var trader = String(doc.RootElement, "dialogId") ?? String(message, "uid");
        return new QuestEvent(at, questId.ToLowerInvariant(), status.Value, eventId, trader);
    }

    // The items a message carries, without their attachments: those whose parent is the message's own stash.
    private static int ItemsIn(JsonElement message)
    {
        if (!Property(message, "items", out var items) || !Property(items, "data", out var data) || data.ValueKind != JsonValueKind.Array)
            return 0;
        var stash = String(items, "stash");
        return data.EnumerateArray().Count(i => stash is null || String(i, "parentId") == stash);
    }

    // A message's "dt" as whole seconds, or null when it isn't a time a DateTimeOffset can hold (a fraction is cut).
    private static long? UnixSeconds(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Number)
            return null;
        if (!e.TryGetInt64(out var seconds))
        {
            if (!e.TryGetDouble(out var value) || double.IsNaN(value) || Math.Abs(value) > MaxUnixSeconds)
                return null;
            seconds = (long)value;
        }
        return seconds is >= MinUnixSeconds and <= MaxUnixSeconds ? seconds : null;
    }

    // The years 1 to 9999, the range DateTimeOffset.FromUnixTimeSeconds takes.
    private const long MinUnixSeconds = -62_135_596_800;
    private const long MaxUnixSeconds = 253_402_300_799;

    // TryGetProperty throws on anything but an object ("items": null, a message that is a string), and one record
    // of a shape nobody expected must not cost the rest of the log.
    private static bool Property(JsonElement e, string name, out JsonElement value)
    {
        value = default;
        return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out value);
    }

    private static JsonDocument? TryParse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? String(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
