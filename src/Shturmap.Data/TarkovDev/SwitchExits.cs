namespace Shturmap.Data.TarkovDev;

/// <summary>
/// The extracts a switch gates, and what leaving through each takes, step by step in the order of use (owner,
/// 2026-10-17: "Some exits still require specific actions, for example one exit in interchange requires the power to be
/// switched on. This is currently not represented in the map at all and should be added as information"; docs/DESIGN.md,
/// "Extract requirements"). Kept by hand: the verification of 2026-10-17 found that tarkov.dev's <c>switches</c> never
/// say it. They are listed on Customs and The Lab only, and there on every extract alike (all 28 of Customs' the one
/// ZB-013 lever, all 7 of The Lab's the Med Elevator's three buttons), so each falls under <see cref="OnMostExtracts"/>
/// and tells no extract apart; Interchange's Saferoom and Reserve's D-2 and bunker list none. A row names the extract by
/// its map's <c>normalizedName</c> and its internal name (<see cref="GameData.ExtractKeys"/>, from before translation)
/// and holds only for the extract of that id; every step has a position in the data, and its name is the data's.
/// <see cref="Drift"/> says where the data no longer has a row's extract or step, and where it now names switches of
/// its own for one (<c>shturmap-cli bring</c>; docs/UPDATES.md). An extract the table doesn't have keeps the data's
/// own switches where they tell it apart (<see cref="FromData"/>; none today).
/// </summary>
public static class SwitchExits
{
    /// <summary>One step of leaving: a switch to flip, or a lock to open on the way (a keycard's panel).</summary>
    /// <param name="Id">The switch's or the lock's id in the map's data.</param>
    public readonly record struct Step(string Id, bool Lock = false)
    {
        /// <summary>Its marker's id on the map ("switch:&lt;id&gt;", "lock:&lt;id&gt;"; MapContentBuilder.Landmarks).</summary>
        public string MarkerId => (Lock ? "lock:" : "switch:") + Id;
    }

    /// <summary>A gated extract: its map's normalizedName, its internal name and id, and its steps in the order of use.</summary>
    public sealed record Row(string Map, string Key, string ExtractId, IReadOnlyList<Step> Steps);

    private static Step S(string id) => new(id);

    private static Step L(string id) => new(id, Lock: true);

    /// <summary>
    /// The ten extracts the verification of 2026-10-17 found gated, with the wiki's account of each (escapefromtarkov.fandom.com,
    /// each map's "Extractions"). The ids are the same in PvE and PvP.
    /// </summary>
    public static IReadOnlyList<Row> Table { get; } =
    [
        // ZB-1013: the ZB-013 lever in Warehouse 4 at the crane yard; then the ZB-1013 key at the door.
        new("customs", "ex_customs_pmc_zb1013", "3f7e27d7bae8ffb6e3004af071b18bf0e7d896b8", [S("ae4bdfc1fc5b30100701158b56ae4d20840e0550")]),
        // Saferoom Exfil: the power at the power station, the urinal in Burger Spot, the Object #11SR keycard at the panel
        // that reveals, the door's button inside. Single use.
        new("interchange", "ex_interchange_pmc_saferoom", "7bdf4ff52b59f4bb8d60cc0ec90a932596b0f6ac",
            [S("b6ebc39ee29cb6659334df309194b696bbfef555"), S("5173fd12df8e7445613e130233e235a94d94b70d"), L("7429efb2dcb7016e715077fb0a686ffdb56fae2e"),
             S("42d13e53ae13417e3ee1dab2606e9b2fa4ff6147")]),
        // D-2: the power lever in the bunker, then the button by the sliding door (the data chains them: "Unlock").
        new("reserve", "EXFIL_Bunker_D2", "b166214093a289801ff6e25389caabe8c83792a5",
            [S("9cad024bc31223f02f7296c3cb1834c5f6fa2fe2"), S("fa1f22e776e4724582eb1dbb18ae864a9303cc5a")]),
        // Bunker Hermetic Door: the lever in the shack west of the white pawn barracks; open for 4 minutes after.
        new("reserve", "EXFIL_Bunker", "819ea4a9c6d61f208739c3cf866c6a17c79b4707", [S("2c4a9ff1d031afefbd0965b189f58e3834cdb367")]),
        // The three elevators: power in the basement (G1, opposite R4, G6), call, extract.
        new("the-lab", "lab_Elevator_Cargo", "aecf93f8def45d42877594c4170f5a71b5b41b39",
            [S("c39cf1e7caac3b830c00b539ee12dfddeb061e2b"), S("8e6aa12e37d1b09a56e1ae19ab203832b0e78df9"), S("558a518a272f611d2e6879b917adb407f428977f")]),
        new("the-lab", "lab_Elevator_Main", "718b3e098cf256525a98ad84c481876082391b18",
            [S("911ef57b76415c1d42fdba3b5c8fdb587dd229a7"), S("72848c177c54e2913e7d0259f23fba929d14ebf5"), S("89170f110a27deb607f63b2382f28c25ba75e861")]),
        new("the-lab", "lab_Elevator_Med", "9f0ea7a638bbcc3e2d855a66ef7a9f643cc06265",
            [S("014e9122e0fad99bcff6f93161712c3c6e1a2a75"), S("d1d16f043cea7be35b2729c5e1e1dd648c6dee13"), S("5077fd224eb1bb588b65e4d3c0b4b5719bdebb66")]),
        // Hangar Gate: the switch in cabin B24 on the hangar's 2nd floor. Parking Gate: Y21, on the 2nd floor by R22.
        new("the-lab", "lab_Hangar_Gate", "e4449767a7b69f57de8efbd2cf7dd5ee11a8468d", [S("40ec0101fbf68e6d1b762a72080b9b9b9c91f6f1")]),
        new("the-lab", "lab_Parking_Gate", "99477cb352520521b92ba91a2c1cfd7fc219ad26", [S("07fe694c3915c1363b5fa1faa604c68fe5849aba")]),
        // Sewage Conduit: the pump button in B1 lowers the water (about 80 s), then the water level switch.
        new("the-lab", "lab_Under_Storage_Collector", "70e59e240344a72e8dbb151c370f32851997fd50",
            [S("a948c3ba250ffcaaa508230d0b3392bf09cb6964"), S("e7096cec97efb58f59f78f048792235f89318a94")]),
    ];

    /// <summary>The table's row for an extract: of its map (its base map's where it took that map's extracts, <see cref="GameData.ExtractMapName"/>:
    /// The Lab (Dark)'s are The Lab's), with its internal name, and its id. Null for any other.</summary>
    public static Row? RowOf(GameData data, ApiMap map, ApiExtract extract)
    {
        var key = data.ExtractKeys.GetValueOrDefault(extract.Id);
        return key is null ? null : Table.FirstOrDefault(r => r.Map == data.ExtractMapName(map) && r.Key == key && r.ExtractId == extract.Id);
    }

    /// <summary>
    /// What leaving through an extract takes, in the order of use: its table row's steps, else the data's own switches
    /// where they tell it apart (<see cref="FromData"/>). Empty for an extract no switch gates.
    /// </summary>
    public static IReadOnlyList<Step> Of(GameData data, ApiMap map, ApiExtract extract) => RowOf(data, map, extract)?.Steps ?? FromData(map, extract);

    /// <summary>
    /// The data's own word on an extract: its <c>switches</c> that tell it apart (not those on most of the map's
    /// extracts, <see cref="OnMostExtracts"/>), and the switches that unlock those, up to four steps through the data's
    /// <c>activates</c> ("Unlock", not "Lock"), the unlocking one before the one it unlocks. Placed or not: a step the
    /// data doesn't place has no marker, but it is still a step.
    /// </summary>
    public static IReadOnlyList<Step> FromData(ApiMap map, ApiExtract extract)
    {
        var direct = (extract.Switches ?? []).Where(s => !OnMostExtracts(map, s)).Distinct(StringComparer.Ordinal).ToList();
        if (direct.Count == 0)
            return [];
        // Who unlocks whom, read backwards: for each switch, the switches whose flipping unlocks it.
        var activators = (map.Switches ?? [])
            .SelectMany(s => (s.Activates ?? []).Where(a => a is { Operation: "Unlock", Switch: not null }).Select(a => (By: s.Id, Of: a.Switch!)))
            .ToLookup(a => a.Of, a => a.By, StringComparer.Ordinal);
        var needed = new HashSet<string>(direct, StringComparer.Ordinal);
        var layers = new List<List<string>> { direct };
        var frontier = direct;
        for (var step = 0; step < 4 && frontier.Count > 0; step++)
        {
            frontier = frontier.SelectMany(s => activators[s]).Where(needed.Add).ToList();
            if (frontier.Count > 0)
                layers.Add(frontier);
        }
        return layers.AsEnumerable().Reverse().SelectMany(l => l).Select(S).ToList();
    }

    /// <summary>
    /// Whether tarkov.dev lists a switch on most of a map's extracts (more than half of them): then it tells none of
    /// them apart. Customs lists one lever on all 28 extracts and The Lab the Med Elevator's three buttons on all 7,
    /// though most of them need no switch. "Most", not "every": with one extract put right in the data, the other 27
    /// would all have lit the lever (the review of 2026-10-04). A map's only extract is told apart by any switch.
    /// </summary>
    public static bool OnMostExtracts(ApiMap map, string switchId)
    {
        var extracts = map.Extracts ?? [];
        return extracts.Count > 1 && extracts.Count(e => e.Switches?.Contains(switchId) == true) * 2 > extracts.Count;
    }

    /// <summary>What a look at the table against the data found (<see cref="Drift"/>).</summary>
    /// <param name="Stale">The table no longer fits the data (an extract or a step gone, an id changed): its exit
    /// loses its badge and line until the row is put right. Otherwise the data now names switches of its own for a
    /// table exit: a fix upstream to compare with the row, which may then go.</param>
    public sealed record Finding(bool Stale, string Text);

    /// <summary>
    /// The table against the data of one mode: every row whose map, extract or step the data no longer has as the row
    /// has it, and every table exit for which the data now names switches of its own (<see cref="FromData"/> not empty).
    /// Empty while the table and the data agree, as on 2026-10-17. English words: for the audit and the tests.
    /// </summary>
    public static IReadOnlyList<Finding> Drift(GameData data)
    {
        var found = new List<Finding>();
        foreach (var row in Table)
        {
            if (data.MapByNormalizedName(row.Map) is not { } map)
            {
                found.Add(new(true, $"{row.Map}: no such map"));
                continue;
            }
            var extract = (map.Extracts ?? []).FirstOrDefault(e => e.Id == row.ExtractId);
            var byKey = (map.Extracts ?? []).FirstOrDefault(e => data.ExtractKeys.GetValueOrDefault(e.Id) == row.Key);
            if (extract is null || data.ExtractKeys.GetValueOrDefault(extract.Id) != row.Key)
            {
                found.Add(new(true, byKey is not null
                    ? $"{row.Map} {row.Key}: its id is {byKey.Id} now, the table has {row.ExtractId}"
                    : $"{row.Map} {row.Key} ({row.ExtractId}): no such extract"));
                continue;
            }
            var name = data.EnglishName(extract.Id, extract.Name);
            foreach (var step in row.Steps)
            {
                var placed = step.Lock
                    ? (map.Locks ?? []).Any(l => l.Id == step.Id && l.Position is not null)
                    : (map.Switches ?? []).Any(s => s.Id == step.Id && s.Position is not null);
                if (!placed)
                    found.Add(new(true, $"{row.Map} {name}: {(step.Lock ? "lock" : "switch")} {step.Id} isn't in the data or has no position"));
            }
            if (FromData(map, extract) is { Count: > 0 } own)
                found.Add(new(false, $"{row.Map} {name}: the data now names {string.Join(" → ", own.Select(s => data.EnglishName(s.Id, (map.Switches ?? []).FirstOrDefault(w => w.Id == s.Id)?.Name ?? s.Id)))}; the table has {row.Steps.Count} steps"));
        }
        return found;
    }
}
