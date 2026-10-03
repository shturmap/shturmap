#if DEVTOOLS
using System.Globalization;
using System.Text;
using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;

namespace Shturmap.Session.Dev;

/// <summary>Where a dev raid takes place: the map as the game's logs name it.</summary>
/// <param name="ScenePath">The scene line's bundle, e.g. "maps/city_preset.bundle".</param>
/// <param name="LocationId">The game's location id, e.g. "TarkovStreets" or "bigmap".</param>
public sealed record DevMap(string NormalizedName, string Name, string ScenePath, string LocationId)
{
    /// <summary>A map from tarkov.dev's data. A map whose scene tarkov.dev doesn't give is named by its location id in
    /// the transit line (local raids) and the match setup (server raids), which is how the app resolves it then.</summary>
    public static DevMap From(string normalizedName, string name, string? scenePath, string nameId) =>
        new(normalizedName, name, string.IsNullOrEmpty(scenePath) ? $"maps/{nameId.ToLowerInvariant()}_preset.bundle" : scenePath, nameId);
}

/// <summary>
/// A fake game folder for the developer view (docs/DESIGN.md §8, "Developer aids"): it writes the lines the game's
/// application and push-notification logs would hold, and screenshot files named as the game names them, into
/// <c>Logs</c> and <c>Screenshots</c> under its root. The app reads that folder like the real game's (as with
/// "--fake-game"), so every step runs through the real parser, tracker and session. Never the real game folder.
/// </summary>
public sealed class FakeGame
{
    private const string Build = "1.1.5.1.47510";

    // The profile ids the log fixtures use: the PMC, and the Scav profile the game numbers one higher.
    public const string PmcProfile = "000000000000000000000003";
    public const string ScavProfile = "000000000000000000000004";

    private readonly object _write = new();
    private readonly Dictionary<string, int> _shotsThisMinute = new(StringComparer.Ordinal);

    public FakeGame(string root, DateTime? started = null)
    {
        Root = root;
        var at = started ?? DateTime.Now;
        var stamp = at.ToString("yyyy.MM.dd_HH-mm-ss", CultureInfo.InvariantCulture);
        Session = Path.Combine(root, "Logs", $"log_{stamp}_{Build}");
        Screenshots = Path.Combine(root, "Screenshots");
        Directory.CreateDirectory(Session);
        Directory.CreateDirectory(Screenshots);
        ApplicationLog = Path.Combine(Session, $"{stamp}_{Build} application_000.log");
        PushLog = Path.Combine(Session, $"{stamp}_{Build} push-notifications_000.log");
    }

    public string Root { get; }

    /// <summary>The log session folder, e.g. <c>Logs\log_2026.01.01_14-00-00_1.1.5.1.47510</c>.</summary>
    public string Session { get; }

    public string Screenshots { get; }

    public string ApplicationLog { get; }

    public string PushLog { get; }

    private const string TemporaryPrefix = "shturmap-devview-";

    /// <summary>A fresh folder under %TEMP% for one developer session.</summary>
    public static FakeGame CreateTemporary() =>
        new(Path.Combine(Path.GetTempPath(), TemporaryPrefix + Guid.NewGuid().ToString("N")[..8]));

    /// <summary>Removes earlier sessions' folders under %TEMP% (only "shturmap-devview-" plus eight hex digits) not
    /// written to for <paramref name="olderThan"/>; one still in use stays.</summary>
    public static void PruneTemporary(TimeSpan olderThan)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(Path.GetTempPath(), TemporaryPrefix + "*"))
            {
                var name = Path.GetFileName(dir);
                if (name.Length != TemporaryPrefix.Length + 8 || !name[TemporaryPrefix.Length..].All(Uri.IsHexDigit)
                    || DateTime.Now - Directory.GetLastWriteTime(dir) < olderThan)
                    continue;
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    // ---- the application log ----

    /// <summary>One application-log line, timestamped now (or at <paramref name="at"/>).</summary>
    public void Log(string message, DateTime? at = null) =>
        Append(ApplicationLog, $"{Stamp(at)}|{Build}|Info|application|{message}\r\n");

    /// <summary>"Session mode: Pve" — the profile the game plays (Regular is PvP, PvpSeason the seasonal mode).</summary>
    public void Mode(GameMode mode) => Log("Session mode: " + mode switch
    {
        GameMode.Pvp => "Regular",
        GameMode.Seasonal => "PvpSeason",
        _ => "Pve",
    });

    /// <summary>The main profile loads: after login, and after every raid (the end of a raid in the log).</summary>
    public void ProfileLoaded() => Log($"PrepareSelectedProfileLocally ProfileId:{PmcProfile} AccountId:0");

    /// <summary>Loading starts: the scene line, then for a raid on the server the match setup naming the profile that
    /// plays (a Scav's is another than the menu's); a local raid's transit line names its location instead.</summary>
    public void LoadingStarts(DevMap map, bool scav, bool local)
    {
        Log("scene preset path:" + map.ScenePath + " rcid:" + Path.GetFileNameWithoutExtension(map.ScenePath) + ".scenespreset.asset");
        if (local)
            Log($"[Transit] Flag:Common, RaidId:{Guid.NewGuid():N}, Count:0, Locations:{map.LocationId} -> ");
        else
            Log($"TRACE-NetworkGameCreate profileStatus: 'Profileid: {(scav ? ScavProfile : PmcProfile)}, Status: Busy, RaidMode: Online, Location: {map.LocationId}, shortId: DEV001'");
    }

    /// <summary>The loading steps a real load logs before the raid starts, as in docs/NEXT.md item 3.</summary>
    public void LoadingSteps()
    {
        Log("LocationLoaded:23.5 real:29.87 diff:6.37");
        Log("GamePrepared:24.1 real:30.6 diff:6.5");
        Log("GameCreated:24.6(0.5) real:31.1(0.5) diff:6.5");
        Log("PlayerSpawnEvent:30.3(5.7) real:37.2(6.1) diff:6.9");
        Log("GamePooled:46.7(16.4) real:53.6(16.4) diff:6.9");
    }

    /// <summary>The raid starts. A local raid's start has zero length in brackets; a server raid's doesn't.</summary>
    public void RaidStarts(bool local)
    {
        Log(local ? "GameStarting:55.73(0) real:62.6(0) diff:6.87" : "GameStarting:80.26(1.7) real:95.46(2.73) diff:15.19");
        Log(local ? "GameStarted:55.73(0) real:62.6(0) diff:6.87" : "GameStarted:90.6(10.33) real:107.49(12.02) diff:16.89");
    }

    /// <summary>The raid ends: the game reloads the main profile.</summary>
    public void RaidEnds() => ProfileLoaded();

    public void MatchingCancelled(bool local) => Log(local ? "Local game matching cancelled." : "Network game matching cancelled.");

    // ---- the push-notification log ----

    private void Notification(string kind, string body) =>
        Append(PushLog, $"{Stamp(null)}|{Build}|Info|push-notifications|Got notification | {kind}\r\n{body}\r\n");

    /// <summary>A quest started, failed or completed: the chat message the game logs (types 10, 11, 12).</summary>
    public void Quest(string questId, QuestLogStatus status, string? traderId = null)
    {
        var type = status switch
        {
            QuestLogStatus.Failed => 11,
            QuestLogStatus.Completed => 12,
            _ => 10,
        };
        var id = $"dev-{questId}-{type}-{Guid.NewGuid():N}"[..40];
        var dt = DateTimeOffset.Now.ToUnixTimeSeconds();
        var text = status switch
        {
            QuestLogStatus.Failed => "quest failed",
            QuestLogStatus.Completed => "quest completed",
            _ => "quest started",
        };
        Notification("ChatMessageReceived",
            "{\r\n  \"type\": \"new_message\",\r\n  \"eventId\": \"" + id + "\",\r\n  \"dialogId\": \"" + (traderId ?? "54cb50c76803fa8b248b4571") + "\",\r\n" +
            "  \"message\": {\r\n    \"_id\": \"" + id + "\",\r\n    \"type\": " + type.ToString(CultureInfo.InvariantCulture) + ",\r\n" +
            "    \"dt\": " + dt.ToString(CultureInfo.InvariantCulture) + ",\r\n    \"text\": \"" + text + "\",\r\n" +
            "    \"templateId\": \"" + questId + " description\"\r\n  }\r\n}");
    }

    /// <summary>The group's leader picked a raid, and the group is ready.</summary>
    public void GroupPick(DevMap map)
    {
        Notification("GroupMatchRaidSettings",
            "{\r\n  \"type\": \"groupMatchRaidSettings\",\r\n  \"raidSettings\": {\r\n    \"location\": \"" + map.LocationId + "\",\r\n" +
            "    \"timeVariant\": \"CURR\",\r\n    \"raidMode\": \"Online\"\r\n  }\r\n}");
        Notification("GroupMatchRaidReady", "{\r\n  \"type\": \"groupMatchRaidReady\"\r\n}");
    }

    // ---- screenshots ----

    /// <summary>
    /// A screenshot as the game names it, with a position, a facing and the raid clock:
    /// <c>2026-01-01[14-00]_-60.00, 3.50, 300.00_0.00000, 0.70711, 0.00000, 0.70711_14.13 (0).png</c>. The file is
    /// empty (the app reads only the name); its creation time is <paramref name="at"/>, which the app takes as the
    /// time of the position.
    /// </summary>
    public string Screenshot(WorldPoint position, double yawDegrees, DateTime? at = null, double raidClockHours = 14.13)
    {
        var when = at ?? DateTime.Now;
        var name = ScreenshotName(position, yawDegrees, when, raidClockHours, NextCounter(when));
        var path = Path.Combine(Screenshots, name);
        lock (_write)
        {
            File.WriteAllBytes(path, []);
            File.SetCreationTime(path, when);
            File.SetLastWriteTime(path, when);
        }
        return path;
    }

    /// <summary>The game's screenshot name for a position: decimal points whatever the locale, the rotation as a
    /// quaternion turning about the vertical by <paramref name="yawDegrees"/> (0 = world +Z, 90 = world +X).</summary>
    public static string ScreenshotName(WorldPoint p, double yawDegrees, DateTime at, double raidClockHours, int counter)
    {
        var half = yawDegrees * Math.PI / 360;
        var (qy, qw) = (Math.Sin(half), Math.Cos(half));
        return string.Format(CultureInfo.InvariantCulture,
            "{0:yyyy-MM-dd}[{0:HH-mm}]_{1:0.00}, {2:0.00}, {3:0.00}_{4:0.00000}, {5:0.00000}, {6:0.00000}, {7:0.00000}_{8:0.00} ({9}).png",
            at, p.X, p.Y, p.Z, 0.0, qy, 0.0, qw, raidClockHours, counter);
    }

    private int NextCounter(DateTime at)
    {
        var minute = at.ToString("yyyy-MM-dd HH-mm", CultureInfo.InvariantCulture);
        lock (_shotsThisMinute)
        {
            _shotsThisMinute.TryGetValue(minute, out var n);
            _shotsThisMinute[minute] = n + 1;
            return n;
        }
    }

    // ---- helpers ----

    private static string Stamp(DateTime? at) => (at ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private void Append(string path, string text)
    {
        lock (_write)
            File.AppendAllText(path, text, new UTF8Encoding(false));
    }
}

/// <summary>Heights for places picked on the map in the developer view.</summary>
public static class DevPlaces
{
    /// <summary>
    /// The height to give a position picked at (x, z) while <paramref name="floor"/> is shown: the middle of that
    /// floor's height band where one of its extents covers the spot (else its first bounded band); on the base map the
    /// base height's middle when the map gives one, else the height of the nearest known place within 60 m (an
    /// extract, a quest place), else 0.
    /// </summary>
    public static double HeightFor(MapDefinition map, MapLayer? floor, double x, double z, IEnumerable<WorldPoint> known)
    {
        if (floor is not null)
        {
            var bands = floor.Extents.Where(e => double.IsFinite(e.Height.Min) && double.IsFinite(e.Height.Max)).ToList();
            var here = bands.FirstOrDefault(e => e.Boxes.Count == 0 || e.Boxes.Any(b => b.Contains(x, z))) ?? bands.FirstOrDefault();
            if (here is not null)
                return Math.Round((here.Height.Min + here.Height.Max) / 2, 2);
        }
        if (floor is null && double.IsFinite(map.BaseHeight.Min) && double.IsFinite(map.BaseHeight.Max))
            return Math.Round((map.BaseHeight.Min + map.BaseHeight.Max) / 2, 2);
        var spot = new WorldPoint(x, 0, z);
        var near = known.Where(p => p.HorizontalDistanceTo(spot) <= 60)
            .Where(p => floor is null ? !map.Layers.Any(l => l.Contains(p)) : floor.Contains(p))
            .OrderBy(p => p.HorizontalDistanceTo(spot))
            .Select(p => (double?)p.Y)
            .FirstOrDefault();
        return Math.Round(near ?? 0, 2);
    }

    /// <summary>The facing from a drag on the map, in world degrees (0 = +Z, 90 = +X), or null for a plain click.</summary>
    public static double? YawOf(double fromX, double fromZ, double toX, double toZ)
    {
        var (dx, dz) = (toX - fromX, toZ - fromZ);
        if (dx * dx + dz * dz < 1e-6)
            return null;
        var yaw = Math.Atan2(dx, dz) * 180 / Math.PI;
        return yaw < 0 ? yaw + 360 : yaw;
    }
}
#endif
