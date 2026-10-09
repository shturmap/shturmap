using Shturmap.Core;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Map;

/// <summary>An objective of an active quest as it concerns one map.</summary>
/// <param name="Places">Where on this map; empty for objectives with no fixed place (kills, hand-ins).</param>
public sealed record ObjectiveOnMap(ApiTask Quest, ApiObjective Objective, IReadOnlyList<WorldPoint> Places, bool Done);

public sealed record MapContent(IReadOnlyList<MapMarker> Markers, IReadOnlyList<MapZone> Zones, IReadOnlyList<ObjectiveOnMap> Objectives)
{
    /// <summary>Where the map's loot containers stand; drawn only on a sheet (<see cref="MapScene.Containers"/>).</summary>
    public IReadOnlyList<WorldPoint> Containers { get; init; } = [];

    /// <summary>Markers that light with a marker pointed at: an extract's switches, a switch's extracts
    /// (<see cref="MapContentBuilder.ExtractSwitchLinks"/>).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Links { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Per quest, the lock groups of the keys it needs on this map (<see cref="MapContentBuilder.QuestKeys"/>).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> QuestKeys { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}

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

        // Landmarks first, so everything else draws over them (owner, 2026-10-03: locked doors with their keys,
        // switches, and on a sheet the containers, all from tarkov.dev's data).
        markers.AddRange(Landmarks(data, map));
        zones.AddRange(Hazards(map));

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

        // Spawns, one marker per spawn zone at the centroid of the zone's points, or per group where the zone lies
        // apart (owner, 2026-10-02: the player needs to know which area has Scavs, where bosses and snipers are, not
        // every spawn point).
        markers.AddRange(SpawnZones(data, map));

        foreach (var questId in activeQuests)
        {
            if (!data.Tasks.TryGetValue(questId, out var quest))
                continue;
            foreach (var objective in quest.Objectives ?? [])
            {
                var done = doneObjectives.Contains(objective.Id);
                var kind = QuestTaxonomy.Classify(objective.Type);
                var optional = Handovers.Optional(quest, objective);
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
                        zone.Position.ToWorld(), quest.Name, quest.Id, kind, optional));
                    if (zone.Outline is { Count: >= 3 } outline)
                        zones.Add(new MapZone($"zone:{objective.Id}:{places.Count}", done ? MarkerKind.ObjectiveDone : MarkerKind.Objective,
                            outline.Select(p => p.ToWorld()).ToList(), quest.Id));
                }

                // "Maybe here" only where the thing has several places: tarkov.dev lists every quest item's spot as a
                // possible location, also where there is just one (owner, 2026-10-06: Population Census's journal had a
                // "?"; 57 of 109 such objectives in PvE have one place). One place is a place like any other.
                var possible = PlacesItCanBe(objective) > 1;
                foreach (var location in objective.PossibleLocations ?? [])
                {
                    if (location.Map is null || !sameArtwork.Contains(location.Map))
                        continue;
                    foreach (var position in location.Positions ?? [])
                    {
                        places.Add(position.ToWorld());
                        markers.Add(new MapMarker($"objective:{objective.Id}:{places.Count}",
                            done ? MarkerKind.ObjectiveDone : possible ? MarkerKind.PossibleLocation : MarkerKind.Objective,
                            position.ToWorld(), quest.Name, quest.Id, kind, optional));
                    }
                }

                var onThisMap = places.Count > 0 || (objective.Maps ?? []).Any(sameArtwork.Contains) ||
                                ((objective.Maps ?? []).Count == 0 && quest.Map is not null && sameArtwork.Contains(quest.Map));
                if (onThisMap)
                    objectives.Add(new ObjectiveOnMap(quest, objective, places, done));
            }
        }
        return new MapContent(markers, zones, objectives)
        {
            Containers = Containers(map),
            Links = ExtractSwitchLinks(map),
            QuestKeys = QuestKeys(objectives, sameArtwork),
        };
    }

    /// <summary>
    /// The keys each quest with something on this map needs here, as lock groups (<see cref="KeyGroup"/>): the
    /// quest's <c>neededKeys</c> for this map and its objectives' own keys, the same sources BRING's "key for …"
    /// reads. A picked or pointed-at quest lights those locks too (owner, 2026-10-03: Golden Swag's trailer park cabin
    /// door didn't light with the quest).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> QuestKeys(IReadOnlyList<ObjectiveOnMap> objectives, IReadOnlySet<string> sameArtwork)
    {
        var keys = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var quest in objectives.GroupBy(o => o.Quest.Id))
        {
            var task = quest.First().Quest;
            var ids = (task.NeededKeys ?? []).Where(k => k.Map is not null && sameArtwork.Contains(k.Map)).SelectMany(k => k.Keys ?? [])
                .Concat(quest.SelectMany(o => (o.Objective.RequiredKeys ?? []).SelectMany(k => k)))
                .Distinct(StringComparer.Ordinal)
                .Select(KeyGroup)
                .ToList();
            if (ids.Count > 0)
                keys[quest.Key] = ids;
        }
        return keys;
    }

    /// <summary>
    /// An extract and the switches it needs light together (owner, 2026-10-03, from the map audit: you have to find
    /// the switch to leave). An extract lists its switches; a switch that unlocks one of those (a power switch freeing
    /// a lever) counts too, up to a few steps. Only switches the data places have markers; the chain runs through the
    /// others. A switch the data lists on most of a map's extracts tells none of them apart and links nothing
    /// (<see cref="OnMostExtracts"/>). Marker ids: "extract:&lt;id&gt;" and "switch:&lt;id&gt;".
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ExtractSwitchLinks(ApiMap map)
    {
        var placed = (map.Switches ?? []).Where(s => s.Position is not null).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var extracts = map.Extracts ?? [];
        var everywhere = extracts.SelectMany(e => e.Switches ?? []).Distinct(StringComparer.Ordinal)
            .Where(s => OnMostExtracts(map, s)).ToHashSet(StringComparer.Ordinal);
        // Who unlocks whom, read backwards: for each switch, the switches whose flipping unlocks it (not those that lock it).
        var activators = (map.Switches ?? [])
            .SelectMany(s => (s.Activates ?? []).Where(a => a is { Operation: "Unlock", Switch: not null }).Select(a => (By: s.Id, Of: a.Switch!)))
            .ToLookup(a => a.Of, a => a.By, StringComparer.Ordinal);
        var links = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        void Link(string a, string b)
        {
            if (!links.TryGetValue(a, out var set))
                links[a] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(b);
        }
        foreach (var extract in extracts)
        {
            var direct = (extract.Switches ?? []).Where(s => !everywhere.Contains(s)).ToList();
            if (extract.Position is null || direct.Count == 0)
                continue;
            var needed = new HashSet<string>(direct, StringComparer.Ordinal);
            var frontier = direct.ToList();
            for (var step = 0; step < 4 && frontier.Count > 0; step++)
                frontier = frontier.SelectMany(s => activators[s]).Where(needed.Add).ToList();
            foreach (var s in needed.Where(placed.Contains))
            {
                Link("extract:" + extract.Id, "switch:" + s);
                Link("switch:" + s, "extract:" + extract.Id);
            }
        }
        return links.ToDictionary(l => l.Key, l => (IReadOnlyList<string>)l.Value.Order(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Whether tarkov.dev lists a switch on most of a map's extracts (more than half of them): then it tells none of
    /// them apart. Customs lists one lever on all 27 extracts and The Lab the Med Elevator's three buttons on all 7,
    /// though most of them need no switch. "Most", as for the extract list's requirement line (ExtractRules.Needs), not
    /// "every": with one extract put right in the data, the other 26 would all have lit the lever (the review of
    /// 2026-10-04). A map's only extract is told apart by any switch.
    /// </summary>
    public static bool OnMostExtracts(ApiMap map, string switchId)
    {
        var extracts = map.Extracts ?? [];
        return extracts.Count > 1 && extracts.Count(e => e.Switches?.Contains(switchId) == true) * 2 > extracts.Count;
    }

    /// <summary>The group a lock's marker carries: pointing at its key (anywhere in the window) lights it.</summary>
    public static string KeyGroup(string keyId) => "key:" + keyId;

    /// <summary>
    /// How many places, on every map, the thing an objective is about can be at (tarkov.dev's possible locations): more
    /// than one makes each a "maybe here" place, with a "?" on the map and a line on the quest's card saying so.
    /// </summary>
    public static int PlacesItCanBe(ApiObjective objective) => (objective.PossibleLocations ?? []).Sum(l => l.Positions?.Count ?? 0);

    /// <summary>The key a lock marker's group names, or null for any other group.</summary>
    public static string? KeyOf(string? group) =>
        group is not null && group.StartsWith("key:", StringComparison.Ordinal) ? group["key:".Length..] : null;

    /// <summary>
    /// The map's locks and switches as markers, from tarkov.dev's data (owner, 2026-10-03). A lock (a door, or a car's
    /// trunk: both need a key) is labelled with its key's short name, as printed on the key ("TGL MO"), and "needs
    /// power" where the data says so; its group is the key, so pointing at the key lights every lock it opens. A
    /// switch is labelled with its name (power, alarm, elevator, trap switches); a name the data leaves untranslated is
    /// just "Switch". Container locks would mark containers, which on a sheet are dots already; the data has none.
    /// </summary>
    public static IReadOnlyList<MapMarker> Landmarks(GameData data, ApiMap map)
    {
        var markers = new List<MapMarker>();
        foreach (var l in map.Locks ?? [])
        {
            if (l.Position is null || l.LockType is not ("door" or "trunk"))
                continue;
            var label = l.Key is { } key ? data.ItemShortName(key) : "Locked";
            if (l.NeedsPower)
                label += " · needs power";
            markers.Add(new MapMarker("lock:" + (l.Id ?? markers.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)), MarkerKind.Lock,
                l.Position.ToWorld(), label, l.Key is { } k ? KeyGroup(k) : null));
        }
        foreach (var s in map.Switches ?? [])
        {
            if (s.Position is null)
                continue;
            var name = s.Name is { Length: > 0 } n && !n.StartsWith("switch_", StringComparison.Ordinal) ? n : "Switch";
            markers.Add(new MapMarker("switch:" + s.Id, MarkerKind.Switch, s.Position.ToWorld(), name));
        }
        return markers;
    }

    /// <summary>
    /// Hazards tarkov.dev outlines: Labyrinth's traps ("hazard"), trap-sized only, up to <see cref="HazardMaxArea"/>,
    /// and minefields ("minefield"), whatever their size. Labyrinth's 18 traps are 2–27 m²; its 19th "hazard" is
    /// 54 × 58 m, below the central hall's floor, and the data doesn't say what it is, so it is left out rather than
    /// hatching the whole hall. Minefields are drawn where the artwork doesn't draw them itself (owner, 2026-10-03):
    /// Woods, Shoreline, Lighthouse, Streets and Terminal have "mines" layers, Customs, Reserve, Interchange and Ground
    /// Zero don't; the renderer leaves them out over artwork that shows them (<see cref="MinefieldGroup"/>,
    /// <see cref="MapArtwork.ShowsMinefields"/>). "sniper" zones are the border snipers' kill zones (owner, 2026-10-03,
    /// from the map audit), not sniper-Scav spawns: all 107 are named "ScavRole/Marksman", five of their eight maps
    /// have no sniper Scavs at all, and they lie at the map's edges, where the artwork of Customs, Ground Zero, Streets
    /// and Interchange draws them as "danger" groups. They are drawn the same way, where the artwork doesn't
    /// (<see cref="SniperZoneGroup"/>, <see cref="MapArtwork.ShowsSniperZones"/>): the hatch means "this area kills you".
    /// </summary>
    public static IReadOnlyList<MapZone> Hazards(ApiMap map)
    {
        var hazards = map.Hazards ?? [];
        var traps = hazards
            .Where(h => h.HazardType == "hazard" && h.Outline is { Count: >= 3 } outline && Area(outline) <= HazardMaxArea)
            .Select((h, i) => new MapZone($"hazard:{i}", MarkerKind.Hazard, h.Outline!.Select(p => p.ToWorld()).ToList()));
        var minefields = hazards
            .Where(h => h.HazardType == "minefield" && h.Outline is { Count: >= 3 })
            .Select((h, i) => new MapZone($"minefield:{i}", MarkerKind.Hazard, h.Outline!.Select(p => p.ToWorld()).ToList(), MinefieldGroup));
        var snipers = hazards
            .Where(h => h.HazardType == "sniper" && h.Outline is { Count: >= 3 })
            .Select((h, i) => new MapZone($"sniper-zone:{i}", MarkerKind.Hazard, h.Outline!.Select(p => p.ToWorld()).ToList(), SniperZoneGroup));
        return [.. traps, .. minefields, .. snipers];
    }

    /// <summary>The group of a minefield's zone, so the renderer can leave it out over artwork that draws minefields.</summary>
    public const string MinefieldGroup = "minefield";

    /// <summary>The group of a border sniper's kill zone, left out over artwork that draws them; labelled "Sniper zone".</summary>
    public const string SniperZoneGroup = "sniper-zone";

    /// <summary>The largest hazard drawn, in m² (a trap; see <see cref="Hazards"/>).</summary>
    public const double HazardMaxArea = 50;

    // A polygon's area on the ground (x, z), shoelace formula.
    private static double Area(IReadOnlyList<ApiPosition> outline)
    {
        var sum = 0.0;
        for (var i = 0; i < outline.Count; i++)
        {
            var (a, b) = (outline[i], outline[(i + 1) % outline.Count]);
            sum += a.X * b.Z - b.X * a.Z;
        }
        return Math.Abs(sum) / 2;
    }

    /// <summary>Where the map's loot containers stand (each place once).</summary>
    public static IReadOnlyList<WorldPoint> Containers(ApiMap map) =>
        (map.LootContainers ?? []).Where(c => c.Position is not null).Select(c => c.Position!.ToWorld()).Distinct().ToList();

    /// <summary>
    /// One marker per spawn zone, at the centroid (the mean of X, Y and Z) of the zone's points: AI Scav zones
    /// (side "scav", category "bot" or "all", not "sniper"), sniper zones (side "scav", categories "bot" and
    /// "sniper") and, per boss, each of its spawn locations. The data gives zones by name; a zone whose points lie
    /// apart is split into its groups (<see cref="Groups"/>), one marker each, so no marker stands where nothing
    /// spawns. A boss marker says the boss's chance on the map, as Plan does, and what applies to this zone when it
    /// is less ("Kollontay 75% · 50% here"; <see cref="SpawnLabel"/>), on the zone's largest group only. Bosses whose
    /// groups have the same centroid (Customs' Stronghold lists the same points for Reshala and Knight) share one marker.
    /// </summary>
    public static IReadOnlyList<MapMarker> SpawnZones(GameData data, ApiMap map)
    {
        var markers = new List<MapMarker>();
        var spawns = (map.Spawns ?? []).Where(s => s.Position is not null && s.Sides?.Contains("scav") == true).ToList();
        foreach (var zone in spawns.Where(s => s.Categories?.Any(c => c is "bot" or "all") == true && s.Categories?.Contains("sniper") != true)
                     .GroupBy(s => s.ZoneName ?? ""))
            foreach (var (group, i) in Groups(Distinct(zone.Select(s => s.Position!.ToWorld()))).Select((g, i) => (g, i)))
                markers.Add(new MapMarker(GroupId("scav:" + zone.Key, i), MarkerKind.ScavSpawn, Centroid(group), ""));
        foreach (var zone in spawns.Where(s => s.Categories?.Contains("bot") == true && s.Categories?.Contains("sniper") == true).GroupBy(s => s.ZoneName ?? ""))
            foreach (var (group, i) in Groups(Distinct(zone.Select(s => s.Position!.ToWorld()))).Select((g, i) => (g, i)))
                markers.Add(new MapMarker(GroupId("sniper:" + zone.Key, i), MarkerKind.SniperSpawn, Centroid(group), "Sniper"));

        // Per boss or AI squad and zone: the label parts and the zone's points (the payload can list one boss several
        // times). Bosses and the AI squads the bosses list carries (owner, 2026-10-03, from the map audit): Rogues,
        // Raiders, cultists, Black Division and AF are as deadly and have spawn zones and chances in the data. Only the
        // AI PMCs (pmcUSEC, pmcBEAR) are left out: they come everywhere and are no squad with a place.
        var zones = new List<(string Mob, string Zone, List<SpawnPart> Parts, List<WorldPoint> Points)>();
        // A boss's or squad's chance on the map is its likeliest entry's, as Plan says it (GameData.BossMobsOn): The
        // Lab lists Raider groups from 60 % down to 35 %.
        var onMap = (map.Bosses ?? []).GroupBy(b => b.Mob).ToDictionary(g => g.Key, g => Percent(g.Max(b => b.SpawnChance)));
        foreach (var boss in map.Bosses ?? [])
        {
            if (boss.Mob is "pmcUSEC" or "pmcBEAR")
                continue;
            var name = data.Mobs.TryGetValue(boss.Mob, out var mob) ? mob.Name : boss.Mob;
            var chance = onMap[boss.Mob];
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
                // What the data gives this zone, when it is less than the chance on the map: the zone's share of a spawn
                // with several zones, or a group's own chance where likelier groups spawn elsewhere.
                int? here = location.Chance < 0.995 ? Percent(location.Chance)
                    : Percent(boss.SpawnChance) < chance ? Percent(boss.SpawnChance)
                    : null;
                var part = new SpawnPart(name, chance, here);
                if (!zones[index].Parts.Contains(part))
                    zones[index].Parts.Add(part);
                zones[index].Points.AddRange(positions.Select(p => p.ToWorld()));
            }
        }
        var placed = new List<(WorldPoint At, List<string> Mobs, List<SpawnPart> Parts, string Id)>();
        foreach (var zone in zones)
        {
            foreach (var (group, i) in Groups(Distinct(zone.Points)).Select((g, i) => (g, i)))
            {
                var at = Centroid(group);
                // The zone's share is said once, on its largest group.
                List<SpawnPart> parts = i == 0 ? zone.Parts : [];
                var same = placed.FindIndex(p => p.At.HorizontalDistanceTo(at) < 3 && Math.Abs(p.At.Y - at.Y) < 3);
                if (same < 0)
                {
                    placed.Add((at, [zone.Mob], [.. parts], GroupId($"boss:{zone.Mob}:{zone.Zone}", i)));
                    continue;
                }
                if (!placed[same].Mobs.Contains(zone.Mob))
                    placed[same].Mobs.Add(zone.Mob);
                placed[same].Parts.AddRange(parts.Where(p => !placed[same].Parts.Contains(p)));
            }
        }
        foreach (var p in placed)
            markers.Add(new MapMarker(p.Id, MarkerKind.BossSpawn, p.At, SpawnLabel(p.Parts), BossGroup(p.Mobs)));
        return markers;
    }

    /// <summary>One entry of a boss or AI squad at a place: its name, its chance on the map, and what the data gives
    /// this place when it is less (a zone's share, or a less likely group's own chance), else null.</summary>
    public sealed record SpawnPart(string Name, int Chance, int? Here);

    /// <summary>
    /// A boss marker's label, in one format with Plan's line ("Kollontay 75%"): the name, its chance on the map, and
    /// "· N% here" when less applies to this place: "Kollontay 75% · 50% here", "Reshala 75% · 33% here / Knight
    /// 25%". Several entries of one name at one place say one figure for the place, the highest: The Lab's 2nd floor
    /// has Raider groups at 60, 45 and 35 % ("Raider 60%"), its basement at 45 and 40 % ("Raider 60% · 45% here").
    /// Until 2026-10-09 such a place listed every chance ("Raider 60%, 45%, 35%"), a third format beside these two.
    /// </summary>
    public static string SpawnLabel(IEnumerable<SpawnPart> parts) =>
        string.Join(" / ", parts.GroupBy(p => (p.Name, p.Chance)).Select(g =>
            $"{g.Key.Name} {g.Key.Chance}%" + (g.All(p => p.Here is not null) ? $" · {g.Max(p => p.Here)}% here" : "")));

    // A marker must stand among its points: within 25 m across and 3 m (about a floor) in height of one of them.
    // tarkov.dev's zones can span 450 m (Customs, Interchange) or reach into a bunker (Reserve), and one marker at
    // the mean of all their points stood up to 250 m from the nearest one (owner, 2026-10-02: the centroid, but no
    // marker where nothing spawns).
    private const double GroupReach = 25, GroupFloor = 3;
    // In the gap between two points a floor of height weighs like 17 m across.
    private const double HeightWeight = 5;

    /// <summary>
    /// A zone's points in groups, as few as can be: a group whose centroid doesn't stand among its points (see
    /// <see cref="GroupReach"/>) is cut in two at its widest gap, until every group's does. Largest group first.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<WorldPoint>> Groups(IReadOnlyList<WorldPoint> points)
    {
        var done = new List<IReadOnlyList<WorldPoint>>();
        var open = new Stack<IReadOnlyList<WorldPoint>>();
        if (points.Count > 0)
            open.Push(points);
        while (open.TryPop(out var group))
        {
            var centre = Centroid(group);
            if (group.Any(p => p.HorizontalDistanceTo(centre) <= GroupReach && Math.Abs(p.Y - centre.Y) <= GroupFloor))
            {
                done.Add(group);
                continue;
            }
            var (a, b) = SplitAtWidestGap(group);
            open.Push(b);
            open.Push(a);
        }
        return done.OrderByDescending(g => g.Count).ToList();
    }

    // Cuts the longest edge of the points' minimum spanning tree: the two sides are the groups with the widest gap
    // between them.
    private static (List<WorldPoint> A, List<WorldPoint> B) SplitAtWidestGap(IReadOnlyList<WorldPoint> points)
    {
        static double Gap(WorldPoint a, WorldPoint b) =>
            Math.Sqrt(Math.Pow(a.HorizontalDistanceTo(b), 2) + Math.Pow(HeightWeight * (a.Y - b.Y), 2));
        var n = points.Count;
        var inTree = new bool[n];
        var best = Enumerable.Repeat(double.MaxValue, n).ToArray();
        var from = new int[n];
        best[0] = 0;
        from[0] = -1;
        for (var k = 0; k < n; k++)
        {
            var u = -1;
            for (var v = 0; v < n; v++)
                if (!inTree[v] && (u < 0 || best[v] < best[u]))
                    u = v;
            inTree[u] = true;
            for (var v = 0; v < n; v++)
            {
                if (inTree[v])
                    continue;
                var gap = Gap(points[u], points[v]);
                if (gap < best[v])
                {
                    best[v] = gap;
                    from[v] = u;
                }
            }
        }
        var cut = Enumerable.Range(1, n - 1).MaxBy(v => best[v]);
        // The side of the cut edge that hangs from "cut": every point whose path to the tree's root passes it.
        var side = new bool[n];
        for (var v = 0; v < n; v++)
        {
            var w = v;
            while (w >= 0 && w != cut)
                w = from[w];
            side[v] = w == cut;
        }
        return (Enumerable.Range(0, n).Where(v => !side[v]).Select(v => points[v]).ToList(),
                Enumerable.Range(0, n).Where(v => side[v]).Select(v => points[v]).ToList());
    }

    // The first group keeps the zone's id; further groups count on from 2.
    private static string GroupId(string id, int index) => index == 0 ? id : $"{id}:{index + 1}";

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

    private static int Percent(double share) => (int)Math.Round(share * 100);
}
