using Shturmap.Core;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session;

/// <summary>
/// What an extract or transit needs, in a few words: "Pay 5,000 ₽", "Red Rebel ice pick and paracord, no armored
/// rig", "ZB-013 Power Switch first". From tarkov.dev's fields (transfer items, transit conditions), the hand-kept table
/// of the extracts a switch gates (<see cref="SwitchExits"/>, 2026-10-17: the data's own switches tell no extract apart),
/// the hand-kept facts the data doesn't carry (<see cref="ExitFacts"/>: a key at the door, no backpack, single use; and,
/// muted, <see cref="Notes"/>: whether it may be closed, the times it keeps) and, where nothing else says it, from the
/// extract's internal name (the climbing exits, flare exits) and its English name (flare and co-op exits). Never from the
/// name in the game's language: German has "Mira-Allee" for "Mira Ave (Flare)", so the rule was lost there (review of
/// 2026-10-04). Gold is what to bring or do; muted is what is so of the exit, its availability or its time (owner,
/// 2026-10-17; docs/DESIGN.md, "Extract requirements").
/// </summary>
public static class ExtractRules
{
    private const string Roubles = "5449016a4bdc2d6f028b456f";
    private const string Dollars = "5696686a4bdc2da3298b456a";
    private const string Euros = "569668774bdc2da2298b4568";

    // The climbing exits, by the game's internal name ("Alpinist", "Alpinist_light", "RedRebel_alp").
    private static bool Climb(string key) =>
        key.Contains("Alpinist", StringComparison.OrdinalIgnoreCase) || key.Contains("RedRebel", StringComparison.OrdinalIgnoreCase);

    // A flare exit: "(Flare)" in its English name, or "sniper" as a word of its own in the internal name
    // ("E9_sniper", "customs_sniper_exit"). Not "sniper" anywhere in it: Customs' "Sniper Roadblock" is an ordinary
    // exit, and was told to fire a flare. And the flare exits whose names say nothing of it (FlaresByHand).
    private static bool Flare(string key, string english, string id) =>
        english.Contains("(Flare)", StringComparison.OrdinalIgnoreCase)
        || key.Split('_').Contains("sniper", StringComparer.OrdinalIgnoreCase)
        || FlaresByHand.Contains((key, id));

    /// <summary>
    /// Flare exits whose names don't say so, by internal name and id (the verification of 2026-10-17, from the wiki's
    /// "Extractions" of each map): Icebreaker's helicopter (<c>Heli</c>), which takes its fare and a green flare fired in
    /// its signal area. Both must match: an id tarkov.dev gives anew drops the row until it is put right, and the
    /// <c>bring</c> audit says so (<see cref="FlareDrift"/>).
    /// </summary>
    public static readonly IReadOnlySet<(string Key, string Id)> FlaresByHand = new HashSet<(string Key, string Id)>
    {
        ("Heli", "9bf65c4c24d0153f1542a90ddabadb37198ab9ff"),
    };

    /// <summary>The rows of <see cref="FlaresByHand"/> the data of one mode no longer has as they are kept, in English
    /// words (for the audit and the tests); empty while they agree.</summary>
    public static IReadOnlyList<string> FlareDrift(GameData data) =>
        FlaresByHand.Where(f => data.ExtractKeys.GetValueOrDefault(f.Id) != f.Key)
            .Select(f => $"flare exit {f.Key} ({f.Id}): " + (data.ExtractKeys.FirstOrDefault(k => k.Value == f.Key).Key is { } now
                ? $"its id is {now} now"
                : "no such extract"))
            .ToList();

    private static bool CoOp(string english) => english.Contains("(Co-op)", StringComparison.OrdinalIgnoreCase);

    /// <returns>The requirement text (empty if none) and an item to picture, if one has to be handed over.</returns>
    public static (string Text, string? ItemId) Needs(GameData data, ApiMap map, ApiExtract extract)
    {
        var parts = new List<string>();
        string? item = null;
        var key = data.ExtractKeys.GetValueOrDefault(extract.Id) ?? "";
        var english = data.EnglishName(extract.Id, extract.Name);

        if (Climb(key))
            parts.Add(SessionTexts.ExtractClimb);
        if (Flare(key, english, extract.Id))
            parts.Add(SessionTexts.ExtractFlare);
        if (CoOp(english))
            parts.Add(SessionTexts.ExtractCoOp);

        if (extract.TransferItem is { Count: > 0 } transfer)
        {
            var amount = transfer.Count.ToString("N0", UiLanguage.Culture);
            switch (transfer.Item)
            {
                case Roubles:
                    parts.Add(SessionTexts.ExtractPayRoubles(amount: amount));
                    break;
                case Dollars:
                    parts.Add(SessionTexts.ExtractPayDollars(amount: amount));
                    break;
                case Euros:
                    parts.Add(SessionTexts.ExtractPayEuros(amount: amount));
                    break;
                default:
                    var name = data.ItemName(transfer.Item);
                    parts.Add(SessionTexts.ExtractHandOver(item: transfer.Count > 1 ? SessionTexts.ItemTimes(count: amount, item: name) : name));
                    item = transfer.Item;
                    break;
            }
        }

        // A switch first (the hand-kept table, else the data's own switches where they tell this extract apart): the
        // first step's name, and how many steps there are where there are several ("Mall Main Power Switch first ·
        // 4 steps"; owner, 2026-10-17). The map shows them all while the extract is pointed at.
        if (SwitchExits.Of(data, map, extract) is { Count: > 0 } steps)
        {
            var first = StepName(data, map, steps[0]);
            parts.Add(steps.Count > 1 ? SessionTexts.ExtractSwitchChain(switchName: first, steps: steps.Count) : SessionTexts.ExtractSwitchFirst(switchName: first));
        }

        // What the data doesn't carry (ExitFacts; owner, 2026-10-17): the key at the door, pictured where nothing is handed
        // over (the Saferoom's keycard too, though it is a step of the chain above); no backpack; and, last, single use
        // with how many may leave together ("Pay 5,000 ₽ · single use · up to 4 players").
        if (ExitFacts.RowOf(data, extract, map) is { } facts)
        {
            if (facts.DoorKey is { } doorKey)
            {
                parts.Add(SessionTexts.ExtractDoorKey(key: data.ItemName(doorKey)));
                item ??= doorKey;
            }
            if (facts.NoBackpack)
                parts.Add(SessionTexts.ExtractNoBackpack);
            if (facts.SingleUse)
                parts.Add(facts.UpTo is { } upTo ? SessionTexts.ExtractSingleUseUpTo(count: upTo) : SessionTexts.ExtractSingleUse);
        }
        return (string.Join(" · ", parts.Distinct()), item);
    }

    /// <summary>
    /// What is so of an extract, said muted under what it takes (owner, 2026-10-17; <see cref="ExitFacts"/>): a time it
    /// keeps ("Train from 16 min left · stays 7 min", "from 15 min in", "open for 4 min after the lever", "~80 s after the
    /// pump"), then "may be closed" where the game opens it only in some raids. Empty for an extract with none.
    /// </summary>
    public static IReadOnlyList<ExitNote> Notes(GameData data, ApiMap map, ApiExtract extract)
    {
        if (ExitFacts.RowOf(data, extract, map) is not { } facts)
            return [];
        var notes = new List<ExitNote>();
        switch (facts.Time)
        {
            case ExitFacts.Train train:
                notes.Add(new(SessionTexts.ExtractTrain(minutes: train.FromLeft, stays: train.Stays)) { PassedAtLeft = train.GoneAtLeft });
                break;
            case ExitFacts.OpensAt opens:
                notes.Add(new(SessionTexts.ExtractOpensAt(minutes: opens.MinutesIn)) { PassedAtIn = opens.MinutesIn });
                break;
            case ExitFacts.OpenAfterLever lever:
                notes.Add(new(SessionTexts.ExtractOpenAfterLever(minutes: lever.Minutes)));
                break;
            case ExitFacts.AfterPump pump:
                notes.Add(new(SessionTexts.ExtractAfterPump(seconds: pump.Seconds)));
                break;
        }
        if (facts.MayBeClosed)
            notes.Add(new(SessionTexts.ExtractMayBeClosed) { UntilListed = true });
        return notes;
    }

    /// <summary>
    /// The muted line under an extract's needs as the raid's clock stands, the clock of the raid card's readout (its
    /// MIN IN and MIN LEFT): its notes joined, without "may be closed" once the game's extract list was read this raid
    /// (the list's lit, "??:??:??" and struck-out marks say it then), and whether a window on it has passed (a train's
    /// latest leaving; The Way Up open), when the line steps back further. No cue goes with it (principle 11).
    /// </summary>
    /// <param name="listRead">The game's extract list was read this raid.</param>
    /// <param name="minutesIn">Whole minutes since the raid's start line, or null where the raid's own time isn't known
    /// (outside a raid, a Scav, who joins one under way).</param>
    /// <param name="minutesLeft">Whole minutes left on the readout, or null where none is known or the raid's length is over.</param>
    /// <param name="over">Past the raid's length.</param>
    public static (string Text, bool Passed) NoteLine(IReadOnlyList<ExitNote> notes, bool listRead, int? minutesIn, int? minutesLeft, bool over)
    {
        var shown = notes.Where(n => !(n.UntilListed && listRead)).ToList();
        var passed = shown.Any(n => (n.PassedAtLeft is { } gone && (over || minutesLeft <= gone)) || (n.PassedAtIn is { } open && minutesIn >= open));
        return (string.Join(" · ", shown.Select(n => n.Text)), passed);
    }

    // A step's name as its marker on the map says it (MapContentBuilder.Landmarks): a switch's name from the data,
    // "Switch" where the data leaves it untranslated; a lock's key.
    private static string StepName(GameData data, ApiMap map, SwitchExits.Step step)
    {
        if (step.Lock)
            return (map.Locks ?? []).FirstOrDefault(l => l.Id == step.Id)?.Key is { } key ? data.ItemName(key) : Map.MapTexts.MarkerLocked;
        return (map.Switches ?? []).FirstOrDefault(s => s.Id == step.Id)?.Name is { Length: > 0 } name && !name.StartsWith("switch_", StringComparison.Ordinal)
            ? name
            : Map.MapTexts.MarkerSwitch;
    }

    // Items the rules above name in words: the green signal flare (RSP-30 green cartridge) fired at a flare exit, and
    // the climbing exits' ice pick and paracord. Game item ids, which don't change. Green, not red (the verification of
    // 2026-10-17: the wiki's "Extractions" of all six flare exits, and the RSP-30 green's "Usage"; a 26x75mm green flare
    // from a signal pistol, 62389aaba63f32501b1b444f, does as well). Until then the rule said red.
    public const string GreenFlare = "6217726288ed9f0845317459";
    public const string IcePick = "5c0126f40db834002a125382";
    public const string Paracord = "5c12688486f77426843c7d32";

    /// <summary>
    /// The items leaving through an extract takes, by the same rules as <see cref="Needs(GameData, ApiMap, ApiExtract)"/>:
    /// a green flare at a flare exit, ice pick and paracord at a climbing exit, the money or item a paid exit asks for, the
    /// key to an exit's door (<see cref="ExitFacts"/>). Empty when it takes none (switches and co-op exits take no item).
    /// </summary>
    public static IReadOnlyList<(string ItemId, int Count)> Items(GameData data, ApiExtract extract)
    {
        var items = new List<(string, int)>();
        var key = data.ExtractKeys.GetValueOrDefault(extract.Id) ?? "";
        if (Climb(key))
        {
            items.Add((IcePick, 1));
            items.Add((Paracord, 1));
        }
        if (Flare(key, data.EnglishName(extract.Id, extract.Name), extract.Id))
            items.Add((GreenFlare, 1));
        if (extract.TransferItem is { Count: > 0 } transfer)
            items.Add((transfer.Item, (int)Math.Ceiling(transfer.Count)));
        if (ExitFacts.RowOf(data, extract)?.DoorKey is { } doorKey)
            items.Add((doorKey, 1));
        return items;
    }

    /// <summary>What a transit needs ("TerraGroup Labs access keycard required (1)"), or empty.</summary>
    public static string Needs(ApiTransit transit) => transit.Conditions?.Trim() ?? "";
}
