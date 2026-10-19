using Shturmap.Core.Desk;
using Shturmap.Core.Logs;

namespace Shturmap.App.Rules;

/// <summary>
/// A made-up desk for developer runs and snapshots ("--desk [&lt;n&gt;]", the developer script's "desk [&lt;n&gt;]",
/// tools\fake-raid.ps1 -Desk; docs/DESIGN.md §8, "Developer aids"), built through the model as a reading's page is
/// (<see cref="DeskSheet.Of"/>): real items of the catalogue, so their art and cards are there, with made-up prices,
/// counts, conditions and needs. It shows every mark the view draws: a LOADOUT strip with "?" slots, a scaled condition
/// and weapons with their mods unseen; TACTICAL RIG with a long condition that takes the arrow's room; POCKETS with a
/// wallet whose contents are unseen; a BACKPACK whose "?" Zhuk has its window attached ("ZHUK · IN THE BACKPACK"); the
/// POUCH's secure container, which has no price; a STASH page with "?" cells, keep notches of each weight, arrows, a
/// case with its contents unseen and a cell with no price; and a wallets' window attached to none, split between ON YOU
/// and the stash.
/// </summary>
public static class DeskExample
{
    /// <summary>The readings there are: 1, the mode's first (every region opens); 2, one that changed the backpack (a
    /// LEDX more: the rest folds); 3, the same page again (a reading that changed nothing changes nothing).</summary>
    public const int Readings = 3;

    private const string Therapist = "54cb57776803fa99248b456e";
    private const string Prapor = "54cb50c76803fa8b248b4571";
    private const string Ragman = "5ac3b934156ae10c4430e83c";

    // The items, by their catalogue ids.
    private const string BastionBlack = "66b5f65ca7f72d197e70bcd6", BastionGreen = "66b5f661af44ca0014063c05", BastionMulticam = "66b5f666cad6f002ab7214c2";
    private const string ArmbandRed = "5b3f3ade86f7746b6b790d8e", ArmbandWhite = "5b3f16c486f7747c327f55f7", ArmbandBlue = "5b3f3af486f774679e752c1f",
        ArmbandUsec = "619bdfd4c9546643a67df6fa";
    private const string Trooper = "5c0e655586f774045612eeb2", DogtagBear = "59f32bb586f774757e1e8442", Ak74n = "5644bd2b4bdc2d3b4c8b4572",
        Glock17 = "5a7ae0c351dfba0017554310";
    private const string BlackRock = "5648a69d4bdc2ded0b8b457b", Analgin = "544fb37f4bdc2dee738b4567", Ifak = "590c678286f77426c9660122",
        Ai2 = "5755356824597772cb798962";
    private const string LabsBlue = "5c1d0c5f86f7744bb2683cf0", LabsViolet = "5c1e495a86f7743109743dfb", LabsYellow = "5c1d0d6d86f7744bb2683e1f",
        LabsRed = "5c1d0efb86f7744baf2e7b7b", Wallet = "60b0f6c058e0b0481a09ad11";
    private const string Blackjack = "5c0e774286f77468413cc5b2", ZhukBlack = "603648ff5a45383c122086ac", ZhukSurpat = "6040dd4ddcf9592f401632d2",
        Gpu = "57347ca924597744596b4e71", Grizzly = "590c657e86f77412b013051d", Salewa = "544fb45d4bdc2dee738b4568",
        Moonshine = "5d1b376e86f774252519444e", Roubles = "5449016a4bdc2d6f028b456f", Bitcoin = "59faff1d86f7746c51718c9c";
    private const string Gamma = "5857a8bc2459772bad15db29", Ledx = "5c0530ee86f774697952d952", ItemCase = "59fb042886f7746c5005a7b2",
        StreamerCase = "66bc98a01a47be227a5e956e", AmmoBs = "56dff026d2720bb8668b4567";

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
        Item(LabsBlue, 1, 1, 433_000, 900_000, Therapist, 300_000, change: 4),
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
    }.ToDictionary(f => f.Id, StringComparer.Ordinal);

    private static DeskItemFacts Item(string id, int w, int h, double? low, double basePrice, string? trader, double paid,
        DeskItemKind kind = DeskItemKind.None, double? change = null, bool noFlea = false) => new(id)
        {
            W = w,
            H = h,
            Kind = kind,
            NoFlea = noFlea,
            LastLow = low,
            BasePrice = basePrice,
            Change48hPercent = change,
            Offers = low is null ? null : 60,
            Traders = trader is null ? [] : [(trader, paid)],
            TraderSells = trader is not null && !noFlea,
        };

    /// <summary>The made-up needs: an active quest's LEDX and blue keycard (now), the quest it unlocks a red keycard and
    /// two graphics cards (next), one further down a moonshine found in raid (later, marked) and a Salewa (later, not
    /// marked: it can be bought), a hideout module's bitcoin.</summary>
    public static DeskKeep Keep { get; } = DeskKeep.Of(
        [
            new DeskQuestNeeds("example-now", [], [(Ledx, 1, true), (LabsBlue, 1, false)]),
            new DeskQuestNeeds("example-next", ["example-now"], [(Gpu, 2, false), (LabsRed, 1, false)]),
            new DeskQuestNeeds("example-later", ["example-next"], [(Moonshine, 1, true), (Salewa, 1, false)]),
        ],
        new HashSet<string> { "example-now" }, new HashSet<string>(),
        [new DeskHideoutNeed("example-station", 2, Bitcoin, 1, false)]);

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

    /// <summary>The page of reading <paramref name="reading"/> (1 to <see cref="Readings"/>).</summary>
    public static DeskPage Page(int reading)
    {
        var at = new DateTimeOffset(2026, 1, 1, 14, 2, 0, TimeSpan.Zero);
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
        if (reading >= 2)
            pack.Add(At(3, 4, Ledx, fir: true));
        var backpack = new DeskRegion("backpack", DeskRegionKind.Backpack, 6, 5, pack);
        var zhuk = new DeskRegion(DeskRegion.WindowKey("Zhuk"), DeskRegionKind.ContainerWindow, 4, 3,
            [At(0, 0, AmmoBs, count: 120), At(1, 0, Analgin, print: new DeskPrint(1, 4)), At(2, 0, LabsViolet), At(0, 1, Gpu)])
        {
            Title = "Zhuk",
            TitleIds = [ZhukBlack, ZhukSurpat],
            AttachedTo = new DeskAnchor("backpack", 0, 0),
            Parts = [new(0, 0, 1, 1), new(1, 0, 1, 1), new(2, 0, 1, 1), new(3, 0, 1, 1), new(0, 1, 2, 2), new(2, 1, 2, 2)],
        };
        var pouch = new DeskRegion("pouch", DeskRegionKind.Pouch, 3, 3,
            [Slot("pouch", Gamma) with { Holds = DeskHolds.Contents }, At(0, 0, LabsRed), At(1, 0, Bitcoin), At(0, 1, Salewa, print: new DeskPrint(220, 400))]);
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
        return new DeskPage(at, [stash, loadout, rig, pockets, special, backpack, zhuk, pouch, wallets]);
    }

    /// <summary>The made-up desk of reading <paramref name="reading"/> (1 to <see cref="Readings"/>), as the session
    /// would publish it, with the window's reading counter.</summary>
    /// <param name="counter">The reading counter to give it (<see cref="DeskView.Reading"/>): one past the last the window saw.</param>
    /// <param name="traderName">A trader's name by id, from the data.</param>
    public static DeskView View(int reading, GameMode mode, int counter, Func<string, string>? traderName, DateTimeOffset now)
    {
        reading = Math.Clamp(reading, 1, Readings);
        var page = Page(reading);
        var sheet = DeskSheet.Of(page, id => Facts.GetValueOrDefault(id), DeskFleaRules.Default, Keep, default, traderName);
        IReadOnlySet<string> changed = reading switch
        {
            1 => page.Regions.Select(r => r.Key).ToHashSet(StringComparer.Ordinal),
            2 => new HashSet<string>(StringComparer.Ordinal) { "backpack" },
            _ => new HashSet<string>(StringComparer.Ordinal),
        };
        return new DeskView(mode, sheet, changed, reading == 1, counter, now);
    }
}
