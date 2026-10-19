using Shturmap.Core.Desk;

namespace Shturmap.App.Rules;

/// <summary>
/// Made-up readings of the in-raid glance for developer runs and snapshots ("--glance &lt;n&gt;", the developer script's
/// "glance &lt;n&gt;", tools\fake-raid.ps1 -Glance; docs/DESIGN.md §8, "Developer aids"), built through the model as a raid
/// screenshot's reading is (<see cref="DeskSheet.Of"/>), from the made-up desk's items, facts and needs
/// (<see cref="DeskExample"/>): the player's own gear on both (ON YOU, never drawn on the plate), and the LOOT pane of 1, a
/// drawer with three items (a graphics card, a wallet whose contents are unseen, a keycard that is one of three); of 2, a
/// body: its slots (a helmet and an armband that are "?", body armour at 98/147, a dog tag, an AK and a Glock with their
/// mods unseen), its rig UNSEARCHED, its pockets SEARCHING with one item found, and its backpack searched (a LEDX, a
/// Grizzly at 1469/1800, a moonshine, roubles, a bitcoin, a graphics card). Nobody's raid: nothing of it is a screenshot.
/// </summary>
public static class GlanceExample
{
    /// <summary>The readings there are: 1, the drawer; 2, the body.</summary>
    public const int Readings = 2;

    private static DeskCell Slot(string slot, params string[] ids) => new(0, 0, 0, 0, ids) { Slot = slot };

    private static DeskCell At(int i, int j, string id, int count = 1, DeskPrint? print = null, bool? fir = null)
    {
        var facts = DeskExample.Facts[id];
        var holds = (facts.Kind & DeskItemKind.Gun) != 0 ? DeskHolds.Mods : (facts.Kind & DeskItemKind.Container) != 0 ? DeskHolds.Contents : DeskHolds.Nothing;
        return new DeskCell(i, j, facts.W, facts.H, [id]) { Count = count, Print = print, FoundInRaid = fir, Holds = holds };
    }

    /// <summary>The page of reading <paramref name="reading"/> (1 or 2): the player's own regions of the made-up desk, and
    /// the LOOT pane's.</summary>
    public static DeskPage Page(int reading)
    {
        var at = new DateTimeOffset(2026, 1, 1, 14, 20, 0, TimeSpan.Zero);
        var own = DeskExample.Page(1).Regions.Where(r => r.OnYou).ToList();
        IReadOnlyList<DeskRegion> loot = Math.Clamp(reading, 1, Readings) == 1 ? Drawer() : Body();
        return new DeskPage(at, [.. own, .. loot]);
    }

    // A drawer: a graphics card, a wallet (its contents unseen: "+"), and a keycard that is one of three ("?").
    private static IReadOnlyList<DeskRegion> Drawer() =>
    [
        new DeskRegion(DeskRegion.KeyOf(DeskRegionKind.Loot), DeskRegionKind.Loot, 4, 2,
        [
            At(0, 0, DeskExample.Gpu),
            At(2, 0, DeskExample.Wallet),
            new DeskCell(3, 1, 1, 1, [DeskExample.LabsBlue, DeskExample.LabsViolet, DeskExample.LabsYellow]),
        ])
        {
            Title = "LOOT · DRAWER",
        },
    ];

    // A body: its slots, its rig not searched yet, its pockets being searched, its backpack searched.
    private static IReadOnlyList<DeskRegion> Body()
    {
        const string title = "LOOT · BODY";
        return
        [
            new DeskRegion(DeskRegion.KeyOf(DeskRegionKind.LootSlots), DeskRegionKind.LootSlots, 0, 0,
            [
                Slot("headwear", DeskExample.BastionBlack, DeskExample.BastionGreen, DeskExample.BastionMulticam) with { I = 1 },
                Slot("armband", DeskExample.ArmbandRed, DeskExample.ArmbandWhite, DeskExample.ArmbandBlue, DeskExample.ArmbandUsec) with { I = 3 },
                Slot("bodyarmor", DeskExample.Trooper) with { I = 4, Print = new DeskPrint(98, 147) },
                Slot("dogtag", DeskExample.DogtagBear) with { I = 6 },
                Slot("onsling", DeskExample.Ak74n) with { I = 7, Holds = DeskHolds.Mods },
                Slot("holster", DeskExample.Glock17) with { I = 8, Holds = DeskHolds.Mods },
            ])
            {
                Title = title,
            },
            new DeskRegion(DeskRegion.KeyOf(DeskRegionKind.LootRig), DeskRegionKind.LootRig, 0, 0,
                [Slot("rig", DeskExample.BlackRock) with { Holds = DeskHolds.Contents }])
            {
                Title = title,
                Search = DeskSearch.Unsearched,
            },
            new DeskRegion(DeskRegion.KeyOf(DeskRegionKind.LootPockets), DeskRegionKind.LootPockets, 4, 1,
                [At(1, 0, DeskExample.Analgin, print: new DeskPrint(2, 4))])
            {
                Title = title,
                Search = DeskSearch.Searching,
                Parts = [new(0, 0, 1, 1), new(1, 0, 1, 1), new(2, 0, 1, 1), new(3, 0, 1, 1)],
            },
            new DeskRegion(DeskRegion.KeyOf(DeskRegionKind.LootBackpack), DeskRegionKind.LootBackpack, 5, 4,
            [
                Slot("backpack", DeskExample.Blackjack) with { Holds = DeskHolds.Contents },
                At(0, 0, DeskExample.Grizzly, print: new DeskPrint(1469, 1800)),
                At(2, 0, DeskExample.Gpu),
                At(4, 0, DeskExample.Ledx, fir: true),
                At(2, 1, DeskExample.Moonshine, fir: true),
                At(3, 1, DeskExample.Bitcoin),
                At(0, 2, DeskExample.Roubles, count: 50000),
            ])
            {
                Title = title,
            },
        ];
    }

    /// <summary>The made-up glance of reading <paramref name="reading"/> (1 or 2), as the session would publish it.</summary>
    /// <param name="counter">Its reading counter (<see cref="RaidGlance.Reading"/>): one past the last the window took.</param>
    /// <param name="traderName">A trader's name by id, from the data.</param>
    public static RaidGlance Glance(int reading, int counter, Func<string, string>? traderName, DateTimeOffset takenAt)
    {
        var page = Page(reading);
        var sheet = DeskSheet.Of(page, id => DeskExample.Facts.GetValueOrDefault(id), DeskFleaRules.Default, DeskExample.Keep, default, traderName);
        return new RaidGlance(sheet, page.Regions.FirstOrDefault(r => r.IsLoot)?.Title, takenAt, counter);
    }
}
