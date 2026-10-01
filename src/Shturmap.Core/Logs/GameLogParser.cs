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

    public static GameMode ModeFrom(string raw) => raw.ToLowerInvariant() switch
    {
        "pve" => GameMode.Pve,
        "regular" or "pvp" => GameMode.Pvp,
        var s when s.Contains("season", StringComparison.Ordinal) || s == "szn" => GameMode.Seasonal,
        _ => GameMode.Unknown,
    };

    private static GameEvent? ParseNotification(LogRecord record)
    {
        if (record.Body is null)
            return null;

        if (record.Message.Contains("UserConfirmed", StringComparison.Ordinal))
        {
            var json = TryParse(record.Body);
            if (json is null)
                return null;
            using (json)
            {
                var root = json.RootElement;
                return new MatchSetupEvent(record.Timestamp, String(root, "location"), String(root, "shortId"), String(root, "profileid")?.ToLowerInvariant());
            }
        }

        if (!record.Message.Contains("ChatMessageReceived", StringComparison.Ordinal))
            return null;

        using var doc = TryParse(record.Body);
        if (doc is null || !doc.RootElement.TryGetProperty("message", out var message))
            return null;

        var type = message.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : -1;
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

        var at = message.TryGetProperty("dt", out var dt) && dt.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(dt.GetInt64()).LocalDateTime
            : record.Timestamp;
        var eventId = String(doc.RootElement, "eventId") ?? String(message, "_id") ?? $"{questId}:{status}:{at:O}";
        var trader = String(doc.RootElement, "dialogId") ?? String(message, "uid");
        return new QuestEvent(at, questId.ToLowerInvariant(), status.Value, eventId, trader);
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
