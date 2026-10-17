namespace Shturmap.Data.TarkovDev;

/// <summary>
/// What tarkov.dev's data doesn't say of an extract, kept by hand beside <see cref="SwitchExits"/> (owner, 2026-10-17:
/// "exit facts" as words on the exit's Raid rail row, nothing new on the map; docs/DESIGN.md, "Extract requirements"):
/// the key at an exit's door, no backpack, single use and how many may leave together, whether the game opens it only in
/// some raids, the times it keeps, and one faction the data has wrong. The verification of 2026-10-17 read each map's
/// "Extractions" on the wiki (escapefromtarkov.fandom.com) against the data: none of these is a field of tarkov.dev's
/// extracts (they carry id, name, faction, switch, switches, position, outline, top, bottom, transferItem, size). A row
/// names the extract as <see cref="SwitchExits"/> does, by its map's <c>normalizedName</c> and its internal name
/// (<see cref="GameData.ExtractKeys"/>), and holds only for the extract of its id; the ids are the same in PvE and PvP,
/// and Night Factory and Ground Zero 21+ have ids of their own. <see cref="Drift"/> says where the data no longer has a
/// row's extract or lock, and where it starts carrying a fact itself (<c>shturmap-cli bring</c>; docs/UPDATES.md).
/// </summary>
public static class ExitFacts
{
    /// <summary>A time an extract keeps: by the raid's clock, or after a switch.</summary>
    public abstract record Timing;

    /// <summary>The armored train: it comes with <paramref name="FromLeft"/> to <paramref name="ToLeft"/> minutes left on
    /// the raid's clock and stays <paramref name="Stays"/> minutes.</summary>
    public sealed record Train(int FromLeft, int ToLeft, int Stays) : Timing
    {
        /// <summary>The minutes left when the latest train has gone: from then on the window has passed.</summary>
        public int GoneAtLeft => ToLeft - Stays;
    }

    /// <summary>Open only from this many minutes after the raid's start (The Labyrinth's The Way Up).</summary>
    public sealed record OpensAt(int MinutesIn) : Timing;

    /// <summary>Open for this many minutes after its lever is pulled (Reserve's Bunker Hermetic Door).</summary>
    public sealed record OpenAfterLever(int Minutes) : Timing;

    /// <summary>Usable about this many seconds after its pump is started (The Lab's Sewage Conduit: the water sinks).</summary>
    public sealed record AfterPump(int Seconds) : Timing;

    /// <summary>An extract's facts: its map's normalizedName, its internal name and id, and what the data doesn't say.</summary>
    /// <param name="Map">Its map's <c>normalizedName</c>.</param>
    /// <param name="Key">Its internal name, from before translation.</param>
    /// <param name="ExtractId">Its id in the map's data.</param>
    public sealed record Row(string Map, string Key, string ExtractId)
    {
        /// <summary>The item id of the key that opens the exit's door, or null.</summary>
        public string? DoorKey { get; init; }

        /// <summary>That door's lock in the map's data (its id), which takes <see cref="DoorKey"/>: checked against the data.</summary>
        public string? DoorLock { get; init; }

        /// <summary>Not with a backpack on: the way out is too narrow.</summary>
        public bool NoBackpack { get; init; }

        /// <summary>Once used, it is closed for the rest of the raid.</summary>
        public bool SingleUse { get; init; }

        /// <summary>How many players may leave there together, or null.</summary>
        public int? UpTo { get; init; }

        /// <summary>The game opens it only in some raids (the wiki: "not always available").</summary>
        public bool MayBeClosed { get; init; }

        /// <summary>A time it keeps, or null.</summary>
        public Timing? Time { get; init; }

        /// <summary>Its faction where the data has it wrong ("pmc"), or null: applied as the data loads
        /// (<see cref="ExtractRepairs"/>).</summary>
        public string? Faction { get; init; }
    }

    // The keys at exit doors (the verification of 2026-10-17: each the lock nearest the exit, 4.6 to 13 m away).
    private const string FactoryEmergencyExitKey = "5448ba0b4bdc2d02308b456c";
    private const string Zb1013Key = "6aa2f6565d82e97ee9076820";
    private const string Zb014Key = "591afe0186f77431bd616a11";
    private const string SaferoomKeycard = "5e42c81886f7742a01529f57";

    /// <summary>
    /// The facts the verification of 2026-10-17 found, with the wiki's account of each (escapefromtarkov.fandom.com, each
    /// map's "Extractions"). The car extracts take up to 4 players (Shoreline's 5) and are single use; the trains' times
    /// are the raid clock's minutes left.
    /// </summary>
    public static IReadOnlyList<Row> Table { get; } =
    [
        // Customs. ZB-1013: the ZB-013 lever (SwitchExits), then its key at the door. Dorms V-Ex and the PMC's Old Gas
        // Station are open in some raids only ("green flares = open"). The PMC's rail to Reserve is tagged scav in the data.
        new("customs", "ex_customs_pmc_zb1013", "3f7e27d7bae8ffb6e3004af071b18bf0e7d896b8")
            { DoorKey = Zb1013Key, DoorLock = "b0cdd94e455d12605961c5ae7b759d41c119575c" },
        new("customs", "ex_customs_pmc_auto", "fed921b46204418ef8fa0174468c6886e91325c4") { SingleUse = true, UpTo = 4, MayBeClosed = true },
        new("customs", "ex_customs_pmc_gasstation", "c936f6e98a38c78963a308892b708474c54f4718") { MayBeClosed = true },
        new("customs", "ex_customs_pmc_rail_milbase", "f367bab752ed8474989104613d1151e9434f2bd4") { Faction = "pmc" },
        // Factory and Night Factory: Cellars and Med Tent Gate ("Gate m") open with the Factory emergency exit key.
        new("factory", "Cellars", "f261d229a5b13a3292a4a49dced2a4949dfc342b")
            { DoorKey = FactoryEmergencyExitKey, DoorLock = "d6cacb05b05ce59b1c3a9026fc890eea3be7a63a" },
        new("factory", "Gate m", "4b93f765fc6e0b6379679b3c827074d4e57ff680")
            { DoorKey = FactoryEmergencyExitKey, DoorLock = "2170d1b52aa6d61ee5d3a8f65183be4e0750e873" },
        new("night-factory", "Cellars", "0c8fea1d2d9b2cc660fc83de3f26bb146dde93ef")
            { DoorKey = FactoryEmergencyExitKey, DoorLock = "d80c6f8c6c7e666253b3192fec6f257c5e3cbe2f" },
        new("night-factory", "Gate m", "523d85848f0f3e2b14d15aabc4f5e05fa58b6080")
            { DoorKey = FactoryEmergencyExitKey, DoorLock = "5dd66e37add8ccee43eb906977b665e30e363755" },
        // Woods: ZB-014 takes its key and, like ZB-016, RUAF Gate and Bridge V-Ex, is open in some raids only.
        new("woods", "ZB-014", "c89cb6207811316f70945ed1de3e5e0d9f8cfc96")
            { DoorKey = Zb014Key, DoorLock = "5e30b67950e8bedd77347237ba3cb30fc7566a26", MayBeClosed = true },
        new("woods", "ZB-016", "66f71d2b38d7af05a0fa888343dda2d5df97b314") { MayBeClosed = true },
        new("woods", "RUAF Gate", "69c8400dbe1a154ddc5a4617d3ae2053239f4f33") { MayBeClosed = true },
        new("woods", "South V-Ex", "010b98a982808abfba6d5419a3705cbe05409de1") { SingleUse = true, UpTo = 4, MayBeClosed = true },
        // Shoreline: Road to North V-Ex is always there, for up to 5; Pier Boat in some raids only.
        new("shoreline", "Shorl_V-Ex", "1dc6fca97ac062b2363fe53479748fb0bc6e2d6b") { SingleUse = true, UpTo = 5 },
        new("shoreline", "Pier Boat", "aef6de6ce5e7c898a2a6a65c4433e683f4e06baa") { MayBeClosed = true },
        // Interchange: the Saferoom's keycard is a step of its chain too (SwitchExits); single use.
        new("interchange", "ex_interchange_pmc_saferoom", "7bdf4ff52b59f4bb8d60cc0ec90a932596b0f6ac")
            { DoorKey = SaferoomKeycard, DoorLock = "7429efb2dcb7016e715077fb0a686ffdb56fae2e", SingleUse = true },
        new("interchange", "ex_interchange_pmc_auto", "12448fe61862391cd784b298a6c3081f24de39d6") { SingleUse = true, UpTo = 4, MayBeClosed = true },
        // Reserve: the armored train comes with 16 to 12 min left and stays 7, in some raids only; the Sewer Manhole takes
        // no backpack; the Bunker Hermetic Door is open for 4 min after its lever (SwitchExits).
        new("reserve", "EXFIL_Train", "bd1ef03f5270eb97daeb05b6e4f206895c17860d") { MayBeClosed = true, Time = new Train(16, 12, 7) },
        new("reserve", "EXFIL_vent", "78919c46c8d06b4ea7790a701b383e1cc0dda28e") { NoBackpack = true },
        new("reserve", "EXFIL_Bunker", "819ea4a9c6d61f208739c3cf866c6a17c79b4707") { Time = new OpenAfterLever(4) },
        // The Lab: the Ventilation Shaft takes no backpack; the Cargo Elevator is single use; Hangar and Parking Gate are
        // open in some raids only; the Sewage Conduit's water sinks in about 80 s after the pump (SwitchExits).
        new("the-lab", "lab_Vent", "bb32a5dbbe8116e0765ed80a5ec7980e47676dc6") { NoBackpack = true },
        new("the-lab", "lab_Elevator_Cargo", "aecf93f8def45d42877594c4170f5a71b5b41b39") { SingleUse = true },
        new("the-lab", "lab_Hangar_Gate", "e4449767a7b69f57de8efbd2cf7dd5ee11a8468d") { MayBeClosed = true },
        new("the-lab", "lab_Parking_Gate", "99477cb352520521b92ba91a2c1cfd7fc219ad26") { MayBeClosed = true },
        new("the-lab", "lab_Under_Storage_Collector", "70e59e240344a72e8dbb151c370f32851997fd50") { Time = new AfterPump(80) },
        // Lighthouse: its armored train comes with 20 to 15 min left and stays 7 (always, unlike Reserve's).
        new("lighthouse", "EXFIL_Train", "cc16b27f07697867c3afe8731a5323f8b6db7574") { Time = new Train(20, 15, 7) },
        // Streets of Tarkov: Courtyard and Primorsky Ave Taxi V-Ex are open in some raids only.
        new("streets-of-tarkov", "E8_yard", "dfe286a04cab0aa9063256c15fb11f9d1af61383") { MayBeClosed = true },
        new("streets-of-tarkov", "E7_car", "e565f6d911f327638f03bdd00990cdd8585a4f80") { SingleUse = true, UpTo = 4, MayBeClosed = true },
        // Ground Zero and Ground Zero 21+: Police Cordon V-Ex, always there.
        new("ground-zero", "Sandbox_VExit", "e858220a780a95ffbb923225d7649aa134df8836") { SingleUse = true, UpTo = 4 },
        new("ground-zero-21", "Sandbox_VExit", "60df4348793d4cde35ee2de0865480b5824a8ee6") { SingleUse = true, UpTo = 4 },
        // The Labyrinth: The Way Up opens 15 min after the raid's start (a door's sound says it).
        new("the-labyrinth", "labir_exit", "8eabbd6a5b3d8d277b4acdbfc2ba735ff5a26ae8") { Time = new OpensAt(15) },
    ];

    /// <summary>
    /// The table's row for an extract: of its map (any map where <paramref name="map"/> is null, as for the items a quest's
    /// exit takes; its base map where it took that map's extracts, <see cref="GameData.ExtractMapName"/>), with its internal
    /// name, and its id. Null for any other.
    /// </summary>
    public static Row? RowOf(GameData data, ApiExtract extract, ApiMap? map = null)
    {
        var key = data.ExtractKeys.GetValueOrDefault(extract.Id);
        return key is null ? null : Table.FirstOrDefault(r => r.Key == key && r.ExtractId == extract.Id && (map is null || r.Map == data.ExtractMapName(map)));
    }

    /// <summary>
    /// The fields tarkov.dev's extracts carried on 2026-10-17. A field beyond these may be one of the facts above,
    /// arrived in the data (<see cref="Drift"/>): the rows are then to compare with it.
    /// </summary>
    public static IReadOnlySet<string> KnownFields { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "id", "name", "faction", "switch", "switches", "position", "outline", "top", "bottom", "transferItem", "size",
    };

    /// <summary>What a look at the table against the data found (<see cref="Drift"/>).</summary>
    /// <param name="Stale">The table no longer fits the data (an extract or a lock gone, an id changed, the lock takes
    /// another key): that extract's facts go unsaid until the row is put right. Otherwise the data now carries a fact
    /// itself: a fix upstream to compare with the row, which may then go.</param>
    public sealed record Finding(bool Stale, string Text);

    /// <summary>
    /// The table against the data of one mode: every row whose map, extract or door lock the data no longer has as the
    /// row has it, every faction the data now gives by itself, and fields on extracts that weren't there on 2026-10-17.
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
            if (row.DoorLock is { } lockId && (map.Locks ?? []).FirstOrDefault(l => l.Id == lockId) is var door
                && (door is null || door.Key != row.DoorKey || door.Position is null))
            {
                found.Add(new(true, door is null || door.Position is null
                    ? $"{row.Map} {name}: lock {lockId} isn't in the data or has no position"
                    : $"{row.Map} {name}: lock {lockId} takes {door.Key} now, the table has {row.DoorKey}"));
            }
            // The faction by hand is set as the data loads; it was needed where the loader says so.
            if (row.Faction is { } faction && !data.ExtractRepairs.Any(r => r.Rule == ExtractRepairs.Rule.FactionByHand && r.ExtractId == row.ExtractId)
                && (map.Extracts ?? []).Where(e => e.Id == row.ExtractId).All(e => e.Faction == faction))
                found.Add(new(false, $"{row.Map} {name}: the data now says {faction} by itself; the row's faction may go"));
        }
        if (data.ExtractFields.Except(KnownFields).Order(StringComparer.Ordinal).ToList() is { Count: > 0 } fields)
            found.Add(new(false, $"extracts now carry {string.Join(", ", fields)}: compare with the table"));
        return found;
    }
}
