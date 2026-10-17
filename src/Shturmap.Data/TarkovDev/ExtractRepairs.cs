using System.Globalization;
using Shturmap.Core.Maps;

namespace Shturmap.Data.TarkovDev;

/// <summary>
/// General rules for the defects in tarkov.dev's extracts that the verification of 2026-10-17 found, applied as the data
/// loads, so the map, the rail, the extract list's matching and the rules all see the same extracts (owner, 2026-10-17:
/// rules, not hand fixes, so an upstream fix needs no undoing; docs/DESIGN.md, "Extract requirements", *The data's
/// defects*). Each does nothing where the data is right:
/// <list type="bullet">
/// <item>an extract outside its map's bounds (<c>svgBounds</c>, else <c>bounds</c>) whose internal name is an extract's of
/// another map (one not drawn with the same artwork) is dropped: a copy left behind (Ground Zero's UN Roadblock, Scav
/// Bunker and Pinewood Basement, at Woods' and Streets' places). The bounds alone would drop six real ones, 1.5 to 16 m
/// past the edge (Factory's and Night Factory's Courtyard Gate, Customs' Railroad Passage, Woods' RUAF Gate,
/// Interchange's Path to River, Icebreaker's helicopter): those are kept, and said (<see cref="Rule.OutsideKept"/>);</item>
/// <item>a hand-kept faction (<see cref="ExitFacts.Row.Faction"/>: Customs' PMC rail to Reserve, tagged scav);</item>
/// <item>a map variant's extract with no faction takes the faction of its base map's extract of the same internal name,
/// the nearest where there are several (Night Factory from Factory, Ground Zero 21+ from Ground Zero); without one there
/// it stays shared;</item>
/// <item>a map variant with no extracts takes its base map's (The Lab (Dark) The Lab's), and its switches where it has
/// none, as its artwork is its base's: the extracts keep their ids, so the hand-kept tables find them under the base
/// map's name (<see cref="GameData.ExtractsFrom"/>), and their switch chains have their steps on the map.</item>
/// </list>
/// A variant's base map is the map whose entry in tarkov.dev's maps.json lists it among its <c>altMaps</c>, that is,
/// whose artwork tarkov.dev draws it with (<see cref="MapDefinition.AltKeys"/>: Night Factory under Factory, Ground Zero
/// 21+ under Ground Zero, The Lab (Dark) under The Lab). A map the app draws with another's artwork only because its
/// name starts with that map's (Ground Zero Tutorial, <see cref="GameData.DefinitionFor(string)"/>'s fallback) has no
/// base here, and borrows nothing (coordinator, 2026-10-17).
/// </summary>
public static class ExtractRepairs
{
    public enum Rule
    {
        /// <summary>Dropped: outside its map, with the internal name of another map's extract.</summary>
        Outside,

        /// <summary>Kept: outside its map's bounds, but no other map has an extract of its name (not a repair; said for the audit).</summary>
        OutsideKept,

        /// <summary>Its faction from the base map's extract of the same internal name.</summary>
        FactionFromBase,

        /// <summary>A variant without extracts took its base map's.</summary>
        ExtractsFromBase,

        /// <summary>Its faction from the hand-kept table (<see cref="ExitFacts"/>).</summary>
        FactionByHand,
    }

    /// <summary>What a rule did, in English words for the audit (<c>shturmap-cli bring</c>) and the tests.</summary>
    /// <param name="Map">The map's normalizedName.</param>
    /// <param name="ExtractId">The extract's id, or null where the rule took a map's extracts as a whole.</param>
    /// <param name="From">The base map's normalizedName where a variant took its extracts (<see cref="Rule.ExtractsFromBase"/>).</param>
    public sealed record Done(Rule Rule, string Map, string? ExtractId, string Text, string? From = null);

    /// <summary>The maps with the rules applied, and what each did.</summary>
    /// <param name="keys">Extracts' internal names by id (<see cref="GameData.ExtractKeys"/>).</param>
    /// <param name="english">Extracts' English names by id, for the words (<see cref="GameData.EnglishNames"/>).</param>
    public static (IReadOnlyDictionary<string, ApiMap> Maps, IReadOnlyList<Done> Done) Apply(IReadOnlyDictionary<string, ApiMap> maps,
        IReadOnlyDictionary<string, string> keys, IReadOnlyList<MapDefinition> definitions, IReadOnlyDictionary<string, string>? english = null)
    {
        var done = new List<Done>();
        var result = maps.ToDictionary(m => m.Key, m => m.Value, StringComparer.Ordinal);
        string Key(ApiExtract e) => keys.GetValueOrDefault(e.Id) ?? "";
        string Name(ApiExtract e) => english?.GetValueOrDefault(e.Id) ?? e.Name ?? e.Id;
        string Short(string id) => id.Length > 8 ? id[..8] : id;

        // Outside the map, a copy of another map's extract.
        foreach (var (id, map) in maps)
        {
            if (GameData.DefinitionFor(definitions, map.NormalizedName) is not { } definition || map.Extracts is not { Count: > 0 } extracts)
                continue;
            var box = definition.SvgBounds ?? definition.Bounds;
            var kept = new List<ApiExtract>();
            foreach (var extract in extracts)
            {
                if (extract.Position is not { } p || box.Contains(p.X, p.Z))
                {
                    kept.Add(extract);
                    continue;
                }
                var namesake = maps.Values.FirstOrDefault(other => !ReferenceEquals(other, map)
                    && GameData.DefinitionFor(definitions, other.NormalizedName) != definition
                    && (other.Extracts ?? []).Any(e => Key(e).Length > 0 && Key(e) == Key(extract)));
                var past = Past(box, p.X, p.Z).ToString("0.#", CultureInfo.InvariantCulture);
                if (namesake is not null)
                {
                    done.Add(new(Rule.Outside, map.NormalizedName, extract.Id,
                        $"{map.NormalizedName} {Name(extract)} ({Short(extract.Id)}): dropped, {past} m outside the map and named as an extract of {namesake.NormalizedName}"));
                    continue;
                }
                done.Add(new(Rule.OutsideKept, map.NormalizedName, extract.Id, $"{map.NormalizedName} {Name(extract)} ({Short(extract.Id)}): {past} m outside the map, kept"));
                kept.Add(extract);
            }
            if (kept.Count != extracts.Count)
                result[id] = map with { Extracts = kept };
        }

        // The hand-kept factions.
        foreach (var row in ExitFacts.Table.Where(r => r.Faction is not null))
        {
            if (result.FirstOrDefault(m => m.Value.NormalizedName == row.Map) is not { Value.Extracts: { } extracts } entry)
                continue;
            if (!extracts.Any(e => e.Id == row.ExtractId && Key(e) == row.Key && e.Faction != row.Faction))
                continue;
            var was = extracts.First(e => e.Id == row.ExtractId).Faction ?? "none";
            result[entry.Key] = entry.Value with
            {
                Extracts = extracts.Select(e => e.Id == row.ExtractId && Key(e) == row.Key ? e with { Faction = row.Faction } : e).ToList(),
            };
            done.Add(new(Rule.FactionByHand, row.Map, row.ExtractId,
                $"{row.Map} {Name(extracts.First(e => e.Id == row.ExtractId))} ({row.Key}): {row.Faction} by hand, the data says {was}"));
        }

        // A variant's extracts without a faction: its base map's, by internal name.
        foreach (var (id, map) in result.ToList())
        {
            if (BaseOf(map, result, definitions) is not { Extracts: { Count: > 0 } baseExtracts } baseMap || map.Extracts is not { Count: > 0 } extracts
                || !extracts.Any(e => string.IsNullOrEmpty(e.Faction)))
                continue;
            var fixedExtracts = extracts.Select(e =>
            {
                if (!string.IsNullOrEmpty(e.Faction))
                    return e;
                var same = baseExtracts.Where(b => !string.IsNullOrEmpty(b.Faction) && Key(b).Length > 0 && Key(b) == Key(e)).ToList();
                var from = same.Count > 1 && e.Position is { } p
                    ? same.MinBy(b => b.Position is { } q ? Math.Pow(q.X - p.X, 2) + Math.Pow(q.Y - p.Y, 2) + Math.Pow(q.Z - p.Z, 2) : double.MaxValue)
                    : same.FirstOrDefault();
                if (from is null)
                    return e;
                done.Add(new(Rule.FactionFromBase, map.NormalizedName, e.Id, $"{map.NormalizedName} {Name(e)} ({Short(e.Id)}): {from.Faction}, as on {baseMap.NormalizedName}"));
                return e with { Faction = from.Faction };
            }).ToList();
            result[id] = map with { Extracts = fixedExtracts };
        }

        // A variant without extracts: its base map's, and the switches their chains take where it has none of its own (the
        // same layout; SwitchExits and ExitFacts find the extracts under the base map's name, GameData.ExtractsFrom).
        foreach (var (id, map) in result.ToList())
        {
            if (map.Extracts is { Count: > 0 } || BaseOf(map, result, definitions) is not { Extracts: { Count: > 0 } baseExtracts } baseMap)
                continue;
            var switches = map.Switches is { Count: > 0 } own ? own : baseMap.Switches;
            result[id] = map with { Extracts = baseExtracts, Switches = switches };
            done.Add(new(Rule.ExtractsFromBase, map.NormalizedName, null,
                $"{map.NormalizedName}: no extracts in the data, takes the {baseExtracts.Count} of {baseMap.NormalizedName}"
                + (ReferenceEquals(switches, baseMap.Switches) && switches is { Count: > 0 } ? $" and its {switches.Count} switches" : ""), baseMap.NormalizedName));
        }
        return (result, done);
    }

    /// <summary>
    /// A map variant's base map: the map whose entry in tarkov.dev's maps.json lists the variant among its <c>altMaps</c>
    /// ("night-factory" → "factory", "the-lab-dark" → "the-lab"). Null for a map with an entry of its own, and for one the
    /// app draws with another map's artwork only by the start of its name ("ground-zero-tutorial").
    /// </summary>
    public static ApiMap? BaseOf(ApiMap map, IReadOnlyDictionary<string, ApiMap> maps, IReadOnlyList<MapDefinition> definitions) =>
        definitions.FirstOrDefault(d => d.AltKeys.Any(k => string.Equals(k, map.NormalizedName, StringComparison.OrdinalIgnoreCase))) is { } definition
            ? maps.Values.FirstOrDefault(m => string.Equals(m.NormalizedName, definition.Key, StringComparison.OrdinalIgnoreCase))
            : null;

    // How far a point lies outside a box, in metres along the ground.
    private static double Past(WorldBox box, double x, double z)
    {
        var dx = Math.Max(0, Math.Max(Math.Min(box.X1, box.X2) - x, x - Math.Max(box.X1, box.X2)));
        var dz = Math.Max(0, Math.Max(Math.Min(box.Z1, box.Z2) - z, z - Math.Max(box.Z1, box.Z2)));
        return Math.Sqrt(dx * dx + dz * dz);
    }
}
