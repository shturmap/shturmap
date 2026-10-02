using Shturmap.Core;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Map;

/// <summary>An objective of an active quest as it concerns one map.</summary>
/// <param name="Places">Where on this map; empty for objectives with no fixed place (kills, hand-ins).</param>
public sealed record ObjectiveOnMap(ApiTask Quest, ApiObjective Objective, IReadOnlyList<WorldPoint> Places, bool Done);

public sealed record MapContent(IReadOnlyList<MapMarker> Markers, IReadOnlyList<MapZone> Zones, IReadOnlyList<ObjectiveOnMap> Objectives);

/// <summary>Builds what to draw on a map: extracts, transits, and the active quests' objectives.</summary>
public static class MapContentBuilder
{
    /// <param name="mapId">The map being played (its own extracts are shown).</param>
    /// <param name="activeQuests">Quests in progress; their objectives on this map are drawn.</param>
    /// <param name="doneObjectives">Objective ids already completed.</param>
    public static MapContent Build(GameData data, string mapId, IEnumerable<string> activeQuests, IReadOnlySet<string> doneObjectives)
    {
        var markers = new List<MapMarker>();
        var zones = new List<MapZone>();
        var objectives = new List<ObjectiveOnMap>();
        if (!data.Maps.TryGetValue(mapId, out var map))
            return new MapContent(markers, zones, objectives);
        var sameArtwork = data.MapIdsSharing(map.NormalizedName);

        foreach (var extract in map.Extracts ?? [])
        {
            if (extract.Position is null)
                continue;
            var kind = extract.Faction switch
            {
                "pmc" => MarkerKind.ExtractPmc,
                "scav" => MarkerKind.ExtractScav,
                _ => MarkerKind.ExtractShared,
            };
            markers.Add(new MapMarker("extract:" + extract.Id, kind, extract.Position.ToWorld(), extract.Name ?? "Extract"));
        }

        foreach (var transit in map.Transits ?? [])
        {
            if (transit.Position is null)
                continue;
            var target = transit.Map is not null && data.Maps.TryGetValue(transit.Map, out var t) ? t.Name : "another map";
            markers.Add(new MapMarker("transit:" + transit.Id, MarkerKind.Transit, transit.Position.ToWorld(), "Transit to " + target));
        }

        // Spawns, one marker per spawn zone at the centroid of the zone's points (owner, 2026-10-02: the player needs
        // to know which area has Scavs, where bosses and snipers are, not every spawn point).
        markers.AddRange(SpawnZones(data, map));

        foreach (var questId in activeQuests)
        {
            if (!data.Tasks.TryGetValue(questId, out var quest))
                continue;
            foreach (var objective in quest.Objectives ?? [])
            {
                var done = doneObjectives.Contains(objective.Id);
                var kind = QuestTaxonomy.Classify(objective.Type);
                var places = new List<WorldPoint>();

                foreach (var zone in objective.Zones ?? [])
                {
                    if (zone.Map is null || !sameArtwork.Contains(zone.Map) || zone.Position is null)
                        continue;
                    // The same zone is listed once per map variant; draw it once.
                    if (places.Any(p => p.HorizontalDistanceTo(zone.Position.ToWorld()) < 0.5))
                        continue;
                    places.Add(zone.Position.ToWorld());
                    markers.Add(new MapMarker($"objective:{objective.Id}:{places.Count}", done ? MarkerKind.ObjectiveDone : MarkerKind.Objective,
                        zone.Position.ToWorld(), quest.Name, quest.Id, kind));
                    if (zone.Outline is { Count: >= 3 } outline)
                        zones.Add(new MapZone($"zone:{objective.Id}:{places.Count}", done ? MarkerKind.ObjectiveDone : MarkerKind.Objective,
                            outline.Select(p => p.ToWorld()).ToList(), quest.Id));
                }

                foreach (var location in objective.PossibleLocations ?? [])
                {
                    if (location.Map is null || !sameArtwork.Contains(location.Map))
                        continue;
                    foreach (var position in location.Positions ?? [])
                    {
                        places.Add(position.ToWorld());
                        markers.Add(new MapMarker($"objective:{objective.Id}:{places.Count}", done ? MarkerKind.ObjectiveDone : MarkerKind.PossibleLocation,
                            position.ToWorld(), quest.Name, quest.Id, kind));
                    }
                }

                var onThisMap = places.Count > 0 || (objective.Maps ?? []).Any(sameArtwork.Contains) ||
                                ((objective.Maps ?? []).Count == 0 && quest.Map is not null && sameArtwork.Contains(quest.Map));
                if (onThisMap)
                    objectives.Add(new ObjectiveOnMap(quest, objective, places, done));
            }
        }
        return new MapContent(markers, zones, objectives);
    }

    /// <summary>
    /// One marker per spawn zone, at the centroid (the mean of X, Y and Z) of the zone's points: AI Scav zones
    /// (side "scav", category "bot" or "all", not "sniper"), sniper zones (side "scav", categories "bot" and
    /// "sniper") and, per boss, each of its spawn locations. The data gives zones by name; the points of one zone are
    /// grouped by that name alone. A boss marker says the boss's chance on the map and, when the boss has several
    /// zones, this zone's share ("Kollontay 75% · 50% here"). Bosses whose zones have the same centroid (Customs'
    /// Stronghold lists the same points for Reshala and Knight) share one marker.
    /// </summary>
    public static IReadOnlyList<MapMarker> SpawnZones(GameData data, ApiMap map)
    {
        var markers = new List<MapMarker>();
        var spawns = (map.Spawns ?? []).Where(s => s.Position is not null && s.Sides?.Contains("scav") == true).ToList();
        foreach (var zone in spawns.Where(s => s.Categories?.Any(c => c is "bot" or "all") == true && s.Categories?.Contains("sniper") != true)
                     .GroupBy(s => s.ZoneName ?? ""))
            markers.Add(new MapMarker("scav:" + zone.Key, MarkerKind.ScavSpawn, Centroid(Distinct(zone.Select(s => s.Position!.ToWorld()))), ""));
        foreach (var zone in spawns.Where(s => s.Categories?.Contains("bot") == true && s.Categories?.Contains("sniper") == true).GroupBy(s => s.ZoneName ?? ""))
            markers.Add(new MapMarker("sniper:" + zone.Key, MarkerKind.SniperSpawn, Centroid(Distinct(zone.Select(s => s.Position!.ToWorld()))), "Sniper"));

        // Per boss and zone: the label parts and the zone's points (the payload can list one boss several times).
        var zones = new List<(string Mob, string Zone, List<string> Parts, List<WorldPoint> Points)>();
        foreach (var boss in map.Bosses ?? [])
        {
            if (!boss.Mob.StartsWith("boss", StringComparison.Ordinal))
                continue;
            var name = data.Mobs.TryGetValue(boss.Mob, out var mob) ? mob.Name : boss.Mob;
            foreach (var location in boss.SpawnLocations ?? [])
            {
                if (location.Positions is not { Count: > 0 } positions)
                    continue;
                var key = location.Name ?? "";
                var index = zones.FindIndex(z => z.Mob == boss.Mob && z.Zone == key);
                if (index < 0)
                {
                    zones.Add((boss.Mob, key, [], []));
                    index = zones.Count - 1;
                }
                var part = $"{name} {Percent(boss.SpawnChance)}%" + (location.Chance < 0.995 ? $" · {Percent(location.Chance)}% here" : "");
                if (!zones[index].Parts.Contains(part))
                    zones[index].Parts.Add(part);
                zones[index].Points.AddRange(positions.Select(p => p.ToWorld()));
            }
        }
        var placed = new List<(WorldPoint At, List<string> Mobs, List<string> Parts, string Zone)>();
        foreach (var zone in zones)
        {
            var at = Centroid(Distinct(zone.Points));
            var same = placed.FindIndex(p => p.At.HorizontalDistanceTo(at) < 3 && Math.Abs(p.At.Y - at.Y) < 3);
            if (same < 0)
            {
                placed.Add((at, [zone.Mob], [.. zone.Parts], zone.Zone));
                continue;
            }
            if (!placed[same].Mobs.Contains(zone.Mob))
                placed[same].Mobs.Add(zone.Mob);
            placed[same].Parts.AddRange(zone.Parts.Where(p => !placed[same].Parts.Contains(p)));
        }
        foreach (var p in placed)
            markers.Add(new MapMarker($"boss:{p.Mobs[0]}:{p.Zone}", MarkerKind.BossSpawn, p.At, string.Join(" / ", p.Parts), BossGroup(p.Mobs)));
        return markers;
    }

    /// <summary>The group of a boss marker: "boss:" and the bosses that share it, joined by "+".</summary>
    public static string BossGroup(IEnumerable<string> mobs) => "boss:" + string.Join("+", mobs);

    /// <summary>The bosses of a boss marker's group.</summary>
    public static IReadOnlyList<string> BossesOf(string group) =>
        group.StartsWith("boss:", StringComparison.Ordinal) ? group["boss:".Length..].Split('+') : [];

    /// <summary>The mean of the points, in all three coordinates.</summary>
    public static WorldPoint Centroid(IReadOnlyList<WorldPoint> points) =>
        new(points.Average(p => p.X), points.Average(p => p.Y), points.Average(p => p.Z));

    // The same point listed twice (one zone repeated in the payload) must not pull the centroid toward it.
    private static List<WorldPoint> Distinct(IEnumerable<WorldPoint> points)
    {
        var kept = new List<WorldPoint>();
        foreach (var p in points)
        {
            if (!kept.Any(k => k.HorizontalDistanceTo(p) < 0.5 && Math.Abs(k.Y - p.Y) < 0.5))
                kept.Add(p);
        }
        return kept;
    }

    private static string Percent(double share) => Math.Round(share * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
}
