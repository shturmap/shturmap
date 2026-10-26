using Shturmap.Core.Desk;
using Shturmap.Core.Logs;

namespace Shturmap.App.Rules;

/// <summary>
/// A made-up desk for developer runs and snapshots ("--desk [&lt;n&gt;]", the developer script's "desk [&lt;n&gt;]",
/// tools\fake-raid.ps1 -Desk; docs/DESIGN.md §8, "Developer aids"), built through the model as a reading's page is
/// (<see cref="DeskSheet.Of"/>): real items of the catalogue, so their art and cards are there, with made-up prices,
/// counts, conditions and needs. It shows every mark the view draws: LOADOUT's boxes with "?" slots, a scaled condition,
/// weapons with their mods unseen and empty slots; TACTICAL RIG with a long condition that takes the arrow's room; POCKETS
/// with a wallet whose contents are unseen; a BACKPACK whose "?" Zhuk has its window attached ("ZHUK · IN THE BACKPACK");
/// no POUCH, which no reading shows, so its place is the dashed frame of a region never read (owner, 2026-10-25); a STASH
/// page with "?" cells, keep notches of each weight, arrows, a case with its contents unseen and a cell with no price; and
/// a wallets' window attached to none, split between ON YOU and the stash. Reading 2 shows the backpack alone, so the
/// rest is kept from reading 1, a step dimmer and dated. For the card and the click (step C, 2026-10-19), a cell of each of the copy's states: the stash's LEDX
/// copies, the pumping station's back door key copies over the best net, the pockets' blue keycard is one Therapist pays
/// more for, the 5.45 BS ammo can't be listed, the backpack's Grizzly at 1469/1800 copies a scaled price, and the stash's
/// keycards' "?" waits for a pick; and every item a made-up price history (<see cref="History"/>), so the card's band
/// shows without a request.
/// </summary>
public static class DeskExample
{
    /// <summary>The readings there are: 1, the mode's first (every region read but the POUCH, which none shows); 2, seven
    /// minutes later, one that showed the backpack alone and changed it (a LEDX more: the rest is kept from reading 1,
    /// dimmed and dated); 3, two minutes after, the same screenshot again (a reading that changed nothing changes nothing:
    /// the backpack read anew, the rest as dimmed as before); 4, a first reading of another page, whose POCKETS, SPECIAL SLOTS and STASH
    /// the reading made from their headings alone (no grid and no cell: their frames under a window or under no heading),
    /// so each stands as never read, the dashed frame (<see cref="DeskPageRules.HeadingAlone"/>; UI QA re-check of 94591a5,
    /// F2), and no wallets' window (its wallets were in the pockets and the stash).</summary>
    public const int Readings = 4;

    // When each reading's screenshot was taken (the repository's example day, 2026-01-01).
    private static DateTimeOffset Taken(int reading) => new DateTimeOffset(2026, 1, 1, 14, 2, 0, TimeSpan.Zero).AddMinutes(reading switch
    {
        1 => 0,
        2 => 7,
        3 => 9,
        _ => 0,
    });

    // Reading 4: a first reading whose POCKETS, SPECIAL SLOTS and STASH came from their headings alone.
    private static bool HeadingsAlone(int reading) => reading == 4;

    private const string Therapist = "54cb57776803fa99248b456e";
    private const string Prapor = "54cb50c76803fa8b248b4571";
    private const string Ragman = "5ac3b934156ae10c4430e83c";

    // The items, by their catalogue ids (the in-raid glance's made-up readings take theirs from here too: GlanceExample).
    internal const string BastionBlack = "66b5f65ca7f72d197e70bcd6", BastionGreen = "66b5f661af44ca0014063c05", BastionMulticam = "66b5f666cad6f002ab7214c2";
    internal const string ArmbandRed = "5b3f3ade86f7746b6b790d8e", ArmbandWhite = "5b3f16c486f7747c327f55f7", ArmbandBlue = "5b3f3af486f774679e752c1f",
        ArmbandUsec = "619bdfd4c9546643a67df6fa";
    internal const string Trooper = "5c0e655586f774045612eeb2", DogtagBear = "59f32bb586f774757e1e8442", Ak74n = "5644bd2b4bdc2d3b4c8b4572",
        Glock17 = "5a7ae0c351dfba0017554310";
    internal const string BlackRock = "5648a69d4bdc2ded0b8b457b", Analgin = "544fb37f4bdc2dee738b4567", Ifak = "590c678286f77426c9660122",
        Ai2 = "5755356824597772cb798962";
    internal const string LabsBlue = "5c1d0c5f86f7744bb2683cf0", LabsViolet = "5c1e495a86f7743109743dfb", LabsYellow = "5c1d0d6d86f7744bb2683e1f",
        LabsRed = "5c1d0efb86f7744baf2e7b7b", Wallet = "60b0f6c058e0b0481a09ad11";
    internal const string Blackjack = "5c0e774286f77468413cc5b2", ZhukBlack = "603648ff5a45383c122086ac", ZhukSurpat = "6040dd4ddcf9592f401632d2",
        Gpu = "57347ca924597744596b4e71", Grizzly = "590c657e86f77412b013051d", Salewa = "544fb45d4bdc2dee738b4568",
        Moonshine = "5d1b376e86f774252519444e", Roubles = "5449016a4bdc2d6f028b456f", Bitcoin = "59faff1d86f7746c51718c9c";
    internal const string Gamma = "5857a8bc2459772bad15db29", Ledx = "5c0530ee86f774697952d952", ItemCase = "59fb042886f7746c5005a7b2",
        StreamerCase = "66bc98a01a47be227a5e956e", AmmoBs = "56dff026d2720bb8668b4567", PumpingKey = "593858c486f774253a24cb52";

    /// <summary>The made-up facts of each item: footprint, kind, flea, what traders pay, the 48 h change, the offers.</summary>
    public static IReadOnlyDictionary<string, DeskItemFacts> Facts { get; } = new[]
    {
        Item(BastionBlack, 2, 2, 58_870, 69_444, Ragman, 30_000, DeskItemKind.Armour, 2),
        Item(BastionGreen, 2, 2, 40_000, 65_823, Ragman, 29_000, DeskItemKind.Armour, -4),
        Item(BastionMulticam, 2, 2, 48_999, 62_412, Ragman, 28_000, DeskItemKind.Armour, 1),
        Item(ArmbandRed, 1, 1, 43_300, 894, Ragman, 300),
        Item(ArmbandWhite, 1, 1, 100_000, 729, Ragman, 250),
        Item(ArmbandBlue, 1, 1, 1_000, 573, Ragman, 200),
        Item(ArmbandUsec, 1, 1, 350_000, 50_000, Ragman, 17_000),
        Item(Trooper, 3, 3, 111_111, 55_760, Ragman, 28_000, DeskItemKind.Armour, -3),
        Item(DogtagBear, 1, 1, null, 900, Prapor, 900, noFlea: true),
        Item(Ak74n, 4, 1, 15_000, 23_147, Prapor, 9_000, DeskItemKind.Gun, 1),
        Item(Glock17, 2, 1, 19_999, 6_608, Prapor, 3_500, DeskItemKind.Gun),
        Item(BlackRock, 3, 4, 45_213, 40_700, Ragman, 20_000, DeskItemKind.Container, 3),
        Item(Analgin, 1, 1, 5_000, 4_560, Therapist, 2_400, DeskItemKind.Meds, -12),
        Item(Ifak, 1, 1, 25_000, 19_011, Therapist, 10_000, DeskItemKind.Meds, -11),
        Item(Ai2, 1, 1, 7_000, 4_160, Therapist, 2_000, DeskItemKind.Meds),
        // Therapist pays more than the flea nets at 3 % under its last low (the panel's D: "Therapist pays more: 459 000 ₽").
        Item(LabsBlue, 1, 1, 433_000, 900_000, Therapist, 459_000, change: 4),
        Item(LabsViolet, 1, 1, 330_000, 600_000, Therapist, 200_000),
        Item(LabsYellow, 1, 1, 160_000, 350_000, Therapist, 120_000),
        Item(LabsRed, 1, 1, 1_000_000, 1_650_000, Therapist, 550_000, change: 6),
        Item(Wallet, 1, 1, 46_000, 40_542, Therapist, 20_000, DeskItemKind.Container),
        Item(Blackjack, 5, 7, null, 126_700, Ragman, 63_350, DeskItemKind.Container, noFlea: true),
        Item(ZhukBlack, 4, 3, 37_500, 30_148, Ragman, 15_000, DeskItemKind.Container),
        Item(ZhukSurpat, 4, 3, 29_878, 30_148, Ragman, 15_000, DeskItemKind.Container),
        Item(Gpu, 2, 1, 630_000, 198_000, Therapist, 140_000, change: 14),
        Item(Grizzly, 2, 2, 41_000, 28_034, Therapist, 14_000, DeskItemKind.Meds, 2),
        Item(Salewa, 1, 2, 36_985, 15_090, Therapist, 7_500, DeskItemKind.Meds),
        Item(Moonshine, 1, 2, 380_000, 200_000, Therapist, 140_000, DeskItemKind.FoodDrink, -3),
        Item(Roubles, 1, 1, null, 1, null, 0, noFlea: true),
        Item(Bitcoin, 1, 1, null, 1_118_246, Therapist, 980_000, noFlea: true),
        Item(Gamma, 3, 3, null, 1_850_000, null, 0, DeskItemKind.Container, noFlea: true),
        Item(Ledx, 1, 1, 1_000_000, 970_000, Therapist, 650_000, change: -10.4),
        Item(ItemCase, 4, 4, 1_333_333, 1_229_994, Therapist, 800_000, DeskItemKind.Container),
        Item(StreamerCase, 3, 3, null, 1, null, 0, DeskItemKind.Container, noFlea: true),
        Item(AmmoBs, 1, 1, null, 780, Prapor, 450, noFlea: true),
        // A rare key listed far above its base price: 3 % under its last low is past the price that nets the most.
        Item(PumpingKey, 1, 1, 1_888_888, 34_000, Therapist, 1_200, change: 22, offers: 4, fleaLevel: 25),
    }.ToDictionary(f => f.Id, StringComparer.Ordinal);

    private static DeskItemFacts Item(string id, int w, int h, double? low, double basePrice, string? trader, double paid,
        DeskItemKind kind = DeskItemKind.None, double? change = null, bool noFlea = false, int offers = 60, int? fleaLevel = null) => new(id)
        {
            W = w,
            H = h,
            Kind = kind,
            NoFlea = noFlea,
            LastLow = low,
            BasePrice = basePrice,
            Change48hPercent = change,
            Offers = low is null ? null : offers,
            FleaLevel = noFlea ? null : fleaLevel ?? 15,
            // The day's range: from a little under the last low to a little over it, the LEDX's high an outlier offer (as
            // the item file had it on 2026-10-19: 9,999,999).
            Low24h = low * 0.82,
            High24h = id == Ledx ? 9_999_999 : low * 1.12,
            Avg24h = low * 0.98,
            // What each trader pays: the best one given, Fence's less (tarkov.dev's sell prices include Fence's).
            Traders = trader is null ? [] : [(trader, paid), (Fence, Math.Round(paid * 0.4))],
            TraderSells = trader is not null && !noFlea,
        };

    private const string Fence = "579dc571d53a0658a154fbec";

    // Real quests and hideout stations, so the card names them from the data; the needs and the tree between them are
    // made up.
    private const string PrivateClinic = "5c0be5fc86f774467a116593", HealthCarePrivacy = "5a68661a86f774500f48afb0", Crisis = "60e71c48c1bfa3050473b8e5";
    private const string Medstation = "5d484fcd654e7668ec2ec322", IntelCenter = "5d484fdf654e7600691aadf8";

    /// <summary>The made-up needs: an active quest's LEDX and blue keycard (now; Private Clinic), the quest it unlocks a red
    /// keycard and two graphics cards (next), one further down a moonshine found in raid (later, marked) and a Salewa
    /// (later, not marked: it can be bought), and the hideout's: the LEDX for the Medstation, the bitcoin for the
    /// Intelligence Center.</summary>
    public static DeskKeep Keep { get; } = DeskKeep.Of(
        [
            new DeskQuestNeeds(PrivateClinic, [], [(Ledx, 1, true), (LabsBlue, 1, false)]),
            new DeskQuestNeeds(HealthCarePrivacy, [PrivateClinic], [(Gpu, 2, false), (LabsRed, 1, false)]),
            new DeskQuestNeeds(Crisis, [HealthCarePrivacy], [(Moonshine, 1, true), (Salewa, 1, false), (Ledx, 2, true)]),
        ],
        new HashSet<string> { PrivateClinic }, new HashSet<string>(),
        [new DeskHideoutNeed(Medstation, 3, Ledx, 1, false), new DeskHideoutNeed(IntelCenter, 3, Bitcoin, 1, false)]);

    private static DeskCell Slot(string slot, params string[] ids) => new(0, 0, 0, 0, ids) { Slot = slot };

    private static DeskCell At(int i, int j, string id, int? count = null, DeskPrint? print = null, bool? fir = null)
    {
        var facts = Facts[id];
        return new DeskCell(i, j, facts.W, facts.H, [id]) { Count = count ?? 1, Print = print, FoundInRaid = fir, Holds = HoldsOf(facts) };
    }

    private static DeskHolds HoldsOf(DeskItemFacts facts) =>
        (facts.Kind & DeskItemKind.Gun) != 0 ? DeskHolds.Mods : (facts.Kind & DeskItemKind.Container) != 0 ? DeskHolds.Contents : DeskHolds.Nothing;

    // A cell whose label fits several items alike: "one of N".
    private static DeskCell OneOf(int i, int j, params string[] ids)
    {
        var facts = Facts[ids[0]];
        return new DeskCell(i, j, facts.W, facts.H, ids) { Holds = HoldsOf(facts) };
    }

    /// <summary>The page in hand after reading <paramref name="reading"/> (1 to <see cref="Readings"/>), each region with
    /// when the screenshot that last showed it was taken: from reading 2 on the backpack the reading's, the rest reading
    /// 1's (the view draws those a step dimmer, their heads dated).</summary>
    public static DeskPage Page(int reading)
    {
        reading = Math.Clamp(reading, 1, Readings);
        var at = Taken(reading);
        var loadout = new DeskRegion("loadout", DeskRegionKind.Loadout, 0, 0,
        [
            Slot("headwear", BastionBlack, BastionGreen, BastionMulticam),
            Slot("armband", ArmbandRed, ArmbandWhite, ArmbandBlue, ArmbandUsec),
            Slot("bodyarmor", Trooper) with { Print = new DeskPrint(98, 147) },
            Slot("dogtag", DogtagBear),
            Slot("onsling", Ak74n) with { Holds = DeskHolds.Mods, Print = new DeskPrint(30, 30) },
            Slot("holster", Glock17) with { Holds = DeskHolds.Mods },
        ]);
        var rig = new DeskRegion("rig", DeskRegionKind.Rig, 6, 2,
        [
            Slot("rig", BlackRock) with { Holds = DeskHolds.Contents },
            At(0, 0, Ai2, print: new DeskPrint(100, 100)),
            At(4, 0, Analgin, print: new DeskPrint(2, 4)),
            At(5, 0, Ifak, print: new DeskPrint(166, 300)),
            At(5, 1, AmmoBs, count: 60),
        ])
        {
            Parts = [new(0, 0, 1, 2), new(1, 0, 1, 2), new(2, 0, 1, 2), new(3, 0, 1, 2), new(4, 0, 1, 1), new(5, 0, 1, 1), new(4, 1, 1, 1), new(5, 1, 1, 1)],
        };
        var pockets = new DeskRegion("pockets", DeskRegionKind.Pockets, 4, 1,
            [At(0, 0, LabsBlue), At(1, 0, Wallet), At(2, 0, Analgin, print: new DeskPrint(4, 4))])
        {
            Parts = [new(0, 0, 1, 1), new(1, 0, 1, 1), new(2, 0, 1, 1), new(3, 0, 1, 1)],
        };
        var special = new DeskRegion("special", DeskRegionKind.SpecialSlots, 3, 1, [])
        {
            Parts = [new(0, 0, 1, 1), new(1, 0, 1, 1), new(2, 0, 1, 1)],
        };
        List<DeskCell> pack =
        [
            Slot("backpack", Blackjack) with { Holds = DeskHolds.Contents },
            OneOf(0, 0, ZhukBlack, ZhukSurpat),
            At(4, 0, Gpu),
            At(4, 1, Grizzly, print: new DeskPrint(1469, 1800)),
            At(0, 3, Salewa, print: new DeskPrint(400, 400)),
            At(1, 3, Moonshine, fir: true),
            At(2, 3, Roubles, count: 50000),
            new DeskCell(3, 3, 1, 1, []),
            At(2, 4, Bitcoin),
        ];
        if (reading is 2 or 3)
            pack.Add(At(3, 4, Ledx, fir: true));
        var backpack = new DeskRegion("backpack", DeskRegionKind.Backpack, 6, 5, pack);
        // The Zhuk's window as the game draws it: ten pockets of one by two (QA pass of 2026-10-19, F3); a graphics card
        // stands turned in one.
        var zhuk = new DeskRegion(DeskRegion.WindowKey("Zhuk"), DeskRegionKind.ContainerWindow, 5, 4,
            [At(0, 0, AmmoBs, count: 120), At(1, 0, Analgin, print: new DeskPrint(1, 4)), At(2, 0, LabsViolet),
                new DeskCell(3, 0, 1, 2, [Gpu]), At(1, 2, Salewa, print: new DeskPrint(400, 400))])
        {
            Title = "Zhuk",
            TitleIds = [ZhukBlack, ZhukSurpat],
            AttachedTo = new DeskAnchor("backpack", 0, 0),
            Parts = [.. Enumerable.Range(0, 10).Select(k => new DeskRect(k % 5, k / 5 * 2, 1, 2))],
        };
        var stash = new DeskRegion("stash", DeskRegionKind.Stash, 10, 11,
        [
            At(0, 0, ItemCase),
            At(4, 0, Gpu),
            At(6, 0, Gpu, fir: true),
            At(8, 0, Ledx, fir: true),
            At(9, 0, Bitcoin),
            OneOf(4, 1, LabsBlue, LabsViolet, LabsYellow),
            OneOf(5, 1, ArmbandRed, ArmbandWhite, ArmbandBlue, ArmbandUsec),
            At(6, 1, Moonshine, fir: true),
            At(7, 1, Grizzly, print: new DeskPrint(540, 1800)),
            At(9, 1, Wallet),
            At(0, 4, Ak74n),
            At(4, 3, StreamerCase),
            At(5, 2, PumpingKey),
            At(7, 3, Trooper, print: new DeskPrint(147, 147)),
            At(0, 5, Salewa, print: new DeskPrint(166, 400)),
            At(1, 5, Ifak, print: new DeskPrint(300, 300)),
            At(2, 5, Analgin, print: new DeskPrint(2, 4)),
            At(3, 5, AmmoBs, count: 120),
            At(1, 6, LabsRed),
            At(2, 6, Moonshine),
            At(0, 8, Grizzly, print: new DeskPrint(1800, 1800)),
            At(2, 8, Gpu),
        ]);
        var wallets = new DeskRegion(DeskRegion.WindowKey("WZ"), DeskRegionKind.ContainerWindow, 2, 2,
            [At(0, 0, Roubles, count: 25000), At(1, 0, LabsYellow)])
        {
            Title = "WZ",
            TitleIds = [Wallet],
            SameName = 2,
            SameNameIn = ["pockets", "stash"],
        };
        if (HeadingsAlone(reading))
        {
            // Their headings read, no frame under them: no columns, no rows, no cell.
            DeskRegion Bare(DeskRegion region) => region with { Cols = 0, Rows = 0, Cells = [], Parts = [] };
            return new DeskPage(at, [.. new[] { Bare(stash), loadout, rig, Bare(pockets), Bare(special), backpack, zhuk }.Select(r => r with { ReadAt = at })]);
        }
        // The backpack as this reading showed it; the rest as reading 1 did.
        DeskRegion Read(DeskRegion region) => region with { ReadAt = region.Key == "backpack" ? at : Taken(1) };
        return new DeskPage(at, [.. new[] { stash, loadout, rig, pockets, special, backpack, zhuk, wallets }.Select(Read)]);
    }

    /// <summary>The POUCH the made-up desk never shows (its place on the desk the dashed frame of a region never read), for
    /// the in-raid glance's made-up readings, which carry the player's own regions (<see cref="GlanceExample"/>): the
    /// secure container, which has no price, a red keycard, a bitcoin and a Salewa at 220/400.</summary>
    internal static DeskRegion Pouch() => new("pouch", DeskRegionKind.Pouch, 3, 3,
        [Slot("pouch", Gamma) with { Holds = DeskHolds.Contents }, At(0, 0, LabsRed), At(1, 0, Bitcoin), At(0, 1, Salewa, print: new DeskPrint(220, 400))]);

    /// <summary>The made-up desk of reading <paramref name="reading"/> (1 to <see cref="Readings"/>), as the session
    /// would publish it, with the window's reading counter.</summary>
    /// <param name="counter">The reading counter to give it (<see cref="DeskView.Reading"/>): one past the last the window saw.</param>
    /// <param name="traderName">A trader's name by id, from the data.</param>
    /// <param name="picks">The "?" cells picked on their cards (the developer script's "desk pick", a candidate clicked).</param>
    /// <param name="priced">The flea's prices against the made-up ones (the developer script's "desk prices": new prices,
    /// 1.1 a tenth up), the traders' as they are.</param>
    public static DeskView View(int reading, GameMode mode, int counter, Func<string, string>? traderName, DateTimeOffset now,
        IReadOnlyDictionary<DeskCellKey, string>? picks = null, double priced = 1)
    {
        reading = Math.Clamp(reading, 1, Readings);
        var page = Page(reading);
        if (picks is { Count: > 0 })
        {
            page = page with
            {
                Regions = page.Regions.Select(r => r with
                {
                    Cells = r.Cells.Select(c => picks.TryGetValue(DeskCellKey.Of(r, c), out var pick) && c.Ids.Contains(pick) ? c with { Pick = pick } : c).ToList(),
                }).ToList(),
            };
        }
        Func<string, DeskItemFacts?> facts = priced == 1 ? id => Facts.GetValueOrDefault(id)
            : id => Facts.GetValueOrDefault(id) is { } f
                ? f with { LastLow = f.LastLow * priced, Avg24h = f.Avg24h * priced, Low24h = f.Low24h * priced, High24h = f.High24h * priced }
                : null;
        var sheet = DeskSheet.Of(page, facts, DeskFleaRules.Default, Keep, default, traderName);
        IReadOnlySet<string> changed = reading switch
        {
            1 or 4 => page.Regions.Select(r => r.Key).ToHashSet(StringComparer.Ordinal),
            2 => new HashSet<string>(StringComparer.Ordinal) { "backpack" },
            _ => new HashSet<string>(StringComparer.Ordinal),
        };
        return new DeskView(mode, sheet, changed, reading is 1 or 4, counter, now)
        {
            FactsOf = facts,
            Flea = DeskFleaRules.Default,
        };
    }

    /// <summary>
    /// A made-up price history of an item of the made-up desk, so the card's band shows without a request: a point every
    /// 2 h over the last 30 days (a point a day before), the average wandering around the item's last low by a slow wave
    /// and a quicker ripple of its own, the lowest offer under it; the same for the same item and time. Null for an item
    /// without a flea price.
    /// </summary>
    public static DeskHistory? History(string itemId, DateTimeOffset now)
    {
        if (Facts.GetValueOrDefault(itemId) is not { LastLow: > 0 } facts)
            return null;
        var seed = itemId.Aggregate(17, (h, c) => unchecked(h * 31 + c));
        var phase = (seed & 0xFFFF) / 65536.0 * Math.PI * 2;
        var last = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour / 2 * 2, 8, 0, now.Offset);
        var points = new List<DeskPricePoint>();
        for (var day = 60; day > 30; day--)
            points.Add(Point(last.AddDays(-day), day * 12));
        for (var step = 30 * 12 - 1; step >= 0; step--)
            points.Add(Point(last.AddHours(-2 * step), step));
        return new DeskHistory(points);

        DeskPricePoint Point(DateTimeOffset at, int stepsBack)
        {
            var t = stepsBack / 12.0;
            var wave = Math.Sin(t / 30 * Math.PI * 2 + phase) * 0.08 + Math.Sin(t * 1.7 + phase * 3) * 0.025 - (facts.Change48hPercent ?? 0) / 100 * Math.Min(1, t / 2);
            var avg = facts.LastLow!.Value * (1.02 + wave);
            var spread = 0.06 + 0.03 * Math.Abs(Math.Sin(t * 0.9 + phase));
            return new DeskPricePoint(at, Math.Round(avg), Math.Round(avg * (1 - spread)), 40);
        }
    }
}
