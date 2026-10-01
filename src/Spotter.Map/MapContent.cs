using Spotter.Core;
using Spotter.Data.TarkovDev;

namespace Spotter.Map;

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

        foreach (var questId in activeQuests)
        {
            if (!data.Tasks.TryGetValue(questId, out var quest))
                continue;
            foreach (var objective in quest.Objectives ?? [])
            {
                var done = doneObjectives.Contains(objective.Id);
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
                        zone.Position.ToWorld(), quest.Name, quest.Id));
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
                            position.ToWorld(), quest.Name, quest.Id));
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
}
