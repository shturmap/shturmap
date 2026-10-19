namespace Shturmap.Core.Desk;

// What a cell is worth (owner, 2026-10-19, from a panel: B1, and the addendum: C1a · Da; docs/DESIGN.md §4, "Screen
// anatomy", *The stash desk*, *The cell*): the better of the flea's net at the last low (after the fee) and the best
// trader's price, and which; scaled to a printed condition; "+" where contents are unseen; a range for "one of N". Rules
// without side effects: the session hands them the item file's facts (tarkov.dev, docs/DESIGN.md §3).

/// <summary>What kind of thing an item is, as far as its figure goes.</summary>
[Flags]
public enum DeskItemKind
{
    None = 0,

    /// <summary>It holds a grid of its own (a backpack, a rig, a case, a wallet).</summary>
    Container = 1,

    /// <summary>A weapon: its mods are unseen, and what it prints is its rounds.</summary>
    Gun = 2,

    /// <summary>A magazine: its "60/60" is its rounds, not a condition.</summary>
    Magazine = 4,

    /// <summary>Medical supplies: at a full print the figure is no estimate.</summary>
    Meds = 8,

    /// <summary>Food and drink: as meds.</summary>
    FoodDrink = 16,

    /// <summary>Armour, helmets, plates: always an estimate (the printed maximum isn't the catalogue's).</summary>
    Armour = 32,
}

/// <summary>An item's facts from tarkov.dev's item file, as the desk's values need them.</summary>
/// <param name="Traders">What each trader pays (trader id, roubles), Fence among them.</param>
public sealed record DeskItemFacts(string Id)
{
    public int W { get; init; } = 1;
    public int H { get; init; } = 1;
    public DeskItemKind Kind { get; init; }

    /// <summary>The most of it one cell stacks, or null when the item file doesn't say: a count read past it is a misread
    /// (<see cref="DeskSheet.CellOf"/> takes it as one).</summary>
    public int? StackMax { get; init; }

    /// <summary>The item can't be listed on the flea (tarkov.dev's type "noFlea").</summary>
    public bool NoFlea { get; init; }

    /// <summary>The player level the flea lists the item from ("flea from level 35"); the player's own level is unknown.</summary>
    public int? FleaLevel { get; init; }

    public double? LastLow { get; init; }
    public double? Avg24h { get; init; }
    public double? Low24h { get; init; }
    public double? High24h { get; init; }
    public double? Change48hPercent { get; init; }
    public int? Offers { get; init; }
    public double BasePrice { get; init; }
    public IReadOnlyList<(string Trader, double Price)> Traders { get; init; } = [];

    /// <summary>A trader sells it (with or without a quest or level to reach): with the flea, whether it can be bought.</summary>
    public bool TraderSells { get; init; }

    /// <summary>When tarkov.dev last scanned its prices.</summary>
    public DateTimeOffset? Updated { get; init; }

    /// <summary>It can be bought: on the flea or from a trader. A need for an item that can't be bought marks "later".</summary>
    public bool Buyable => TraderSells || (!NoFlea && LastLow is > 0);
}

/// <summary>The flea market's rules from the game's globals (tarkov.dev's <c>fleaMarket</c>).</summary>
public sealed record DeskFleaRules(double OfferFeeRate, double RequirementFeeRate, bool FoundInRaidRequired = false, int MinPlayerLevel = 15,
    bool Enabled = true)
{
    /// <summary>The rates seen in both modes on 2026-10-19, for when the file carries none.</summary>
    public static DeskFleaRules Default { get; } = new(0.05, 0.05);
}

/// <summary>The hideout's levels that cut the flea's fee (tarkov.dev's fee: Intelligence Center 3 takes 30 %, each
/// Hideout Management level 1 % of that more). Not read from anywhere: 0 unless the player says (a later setting).</summary>
public readonly record struct DeskFeeLevels(int IntelCenter = 0, int HideoutManagement = 0);

public enum DeskSource
{
    None,
    Flea,
    Trader,
}

/// <summary>Why an item has no flea figure.</summary>
public enum DeskNoFlea
{
    /// <summary>It has one.</summary>
    None,

    /// <summary>The game doesn't let it be listed (tarkov.dev's "noFlea").</summary>
    NotAllowed,

    /// <summary>The flea wants it found in raid and the cell's mark wasn't seen.</summary>
    NotFoundInRaid,

    /// <summary>No offer was seen: no last low.</summary>
    NoOffers,

    /// <summary>The flea is off in the game's globals.</summary>
    FleaOff,
}

/// <summary>The 48 h change's arrow: from ±10 % (docs/DESIGN.md §4, *The cell*).</summary>
public enum DeskArrow
{
    None,
    Up,
    Down,
}

/// <summary>The liquidity dot from the offer count: full from 50, half from 10 to 49, a ring under 10.</summary>
public enum DeskLiquidity
{
    Unknown,
    Ring,
    Half,
    Full,
}

/// <summary>
/// One item's figure for one cell: the price the cell shows for its whole stack in roubles (scaled to a printed
/// condition): the flea's listing price at the last low where the flea is the better sale, else the best trader's; its
/// source; what it would be at full condition; and the flea's net beside the best trader's, for the card.
/// </summary>
public sealed record DeskItemValue(string ItemId, DeskSource Source, double Value, double Full, string? Trader)
{
    /// <summary>What the better sale brings, scaled: the flea's net after the fee, or the best trader's price (the tint's
    /// value per slot).</summary>
    public double Worth { get; init; }

    /// <summary>The flea's net for the stack at the last low after the fee, or null when there is no flea figure.</summary>
    public double? FleaNet { get; init; }

    /// <summary>The fee for the stack at the last low.</summary>
    public double? Fee { get; init; }

    /// <summary>The best trader's price for the stack, or null when no trader buys it.</summary>
    public double? TraderPrice { get; init; }

    public DeskNoFlea NoFlea { get; init; }

    /// <summary>An estimate ("~"): scaled to a printed condition below full, armour (its print isn't the catalogue's), or
    /// an item whose condition matters and isn't printed.</summary>
    public bool Estimate { get; init; }

    /// <summary>The printed share it was scaled by (1 when not scaled).</summary>
    public double Scale { get; init; } = 1;
}

/// <summary>A cell's figure: its value (for "one of N", the range of the candidates'), its source, the marks.</summary>
/// <param name="Value">The cell's value in roubles: the item's, or for "one of N" the least of the candidates' (a total
/// counts at least that).</param>
/// <param name="High">For "one of N", the most of the candidates' figures; else <paramref name="Value"/>.</param>
/// <param name="Source">Where the figure comes from: the flea (the price tag) or a trader (the person, or the trader's
/// portrait at 56 DIP); None for "one of N" (its candidates' sources differ).</param>
/// <param name="Trader">The best trader's id when the source is a trader (<see cref="TraderName"/> its name).</param>
public sealed record DeskFigure(DeskSource Source, double Value, double High, string? Trader)
{
    /// <summary>The trader's name in the app's language, when the source is a trader and the session knows it.</summary>
    public string? TraderName { get; init; }

    /// <summary>"~": an estimate from condition.</summary>
    public bool Estimate { get; init; }

    /// <summary>"+": at least this, more inside (contents or mods unseen).</summary>
    public bool Plus { get; init; }

    /// <summary>"one of N": the range is the candidates'.</summary>
    public bool Unsure { get; init; }

    /// <summary>The figure at full condition, when it was scaled.</summary>
    public double Full { get; init; }

    /// <summary>What the better sale brings per cell of the item's footprint (the flea's net or the trader's), for the tint.</summary>
    public double PerSlot { get; init; }

    /// <summary>What the better sale brings for the stack (the flea's net after the fee, or the best trader's price),
    /// scaled; for "one of N" the least of the candidates'. The head line's totals count this, not the listing price the
    /// cell shows: what the page is worth.</summary>
    public double Worth { get; init; }

    /// <summary>The tint behind the art (*The cell*, "Behind the art"): 0 none, 1 from 20k (ink at 5 %), 2 from 100k (10 %),
    /// 3 from 300k ₽ a slot (16 %).</summary>
    public int Tint { get; init; }

    /// <summary>The 48 h change's arrow (*The cell*, bottom left): ↑ or ↓ from ±10 %.</summary>
    public DeskArrow Arrow { get; init; }

    /// <summary>The 48 h change in percent, when one item.</summary>
    public double? Change48h { get; init; }

    /// <summary>The liquidity dot (*The card*): full from 50 offers, half from 10 to 49, a ring under 10.</summary>
    public DeskLiquidity Liquidity { get; init; }

    /// <summary>The item's flea level, when one item (the player's level is unknown: nothing is compared).</summary>
    public int? FleaLevel { get; init; }

    public DeskNoFlea NoFlea { get; init; }

    /// <summary>The flea's net and fee for the stack, and the best trader's, when one item.</summary>
    public double? FleaNet { get; init; }
    public double? Fee { get; init; }
    public double? TraderPrice { get; init; }
}

public static class DeskValues
{
    /// <summary>The value-per-slot steps of the tint, in roubles (docs/DESIGN.md §4, *The cell*).</summary>
    public static readonly double[] TintSteps = [20_000, 100_000, 300_000];

    /// <summary>The 48 h change from which the cell shows an arrow, in percent.</summary>
    public const double ArrowFrom = 10;

    /// <summary>
    /// The flea's fee for listing <paramref name="count"/> of an item at <paramref name="price"/> each, by tarkov.dev's
    /// formula (tarkov-api's <c>fleaMarketFee</c>, read 2026-10-19): with V0 the base price and VR the price,
    /// fee = V0·Ti·4^P0·Q + VR·Tr·4^PR·Q, P0 = log10(V0/VR) (to the power 1.08 when VR &lt; V0), PR = log10(VR/V0) (to the
    /// power 1.08 when VR ≥ V0); Intelligence Center level 3 takes 30 % off, and each Hideout Management level 1 % of
    /// that more; rounded.
    /// </summary>
    public static double Fee(double basePrice, double price, int count, DeskFleaRules flea, DeskFeeLevels levels = default, bool requireAll = false)
    {
        if (price <= 0 || basePrice <= 0 || count <= 0)
            return 0;
        double q = requireAll ? 1 : count;
        var v0 = basePrice * (count / q);
        var vr = price;
        var p0 = Math.Log10(v0 / vr);
        if (vr < v0)
            p0 = Math.Pow(p0, 1.08);
        var pr = Math.Log10(vr / v0);
        if (vr >= v0)
            pr = Math.Pow(pr, 1.08);
        var fee = v0 * flea.OfferFeeRate * Math.Pow(4.0, p0) * q + vr * flea.RequirementFeeRate * Math.Pow(4.0, pr) * q;
        if (levels.IntelCenter >= 3)
        {
            var discount = 0.3 + 0.3 * levels.HideoutManagement * 0.01;
            fee -= fee * discount;
        }
        return Math.Round(fee, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// One item's value on a cell, for the stack, scaled to the printed condition (docs/DESIGN.md §4, *The cell*, Da):
    /// where the flea is the better sale (its net at the last low after the fee at least the best trader's price), the
    /// flea's listing price at the last low as the flea shows it (owner, 2026-10-19: a key showed "~11k" while the flea
    /// listed 15k; the net is the card's line); else the best trader's price. The share printed ("1469/1800") scales the
    /// figure and it stays "~"; a full print ("40/40", "3/3") drops the "~" (owner, 2026-10-19: a key at 40/40 showed
    /// "~11k"), but on armour, whose printed maximum isn't the catalogue's; with no condition printed, armour, meds, food,
    /// drink and weapons are "~" at the full figure. A magazine's or a weapon's print is its rounds, not a condition. The
    /// flea only where it may be listed: not "noFlea", found in raid where the game's globals want it, with a last low. The
    /// item's flea level is carried, never compared (the player's level isn't read).
    /// </summary>
    public static DeskItemValue Of(DeskItemFacts item, int count, DeskPrint? print, bool? foundInRaid, DeskFleaRules flea, DeskFeeLevels levels = default)
    {
        count = Math.Max(1, count);
        var condition = print is { } p && (item.Kind & (DeskItemKind.Magazine | DeskItemKind.Gun)) == 0 ? p : (DeskPrint?)null;
        var scale = condition?.Share ?? 1;
        var estimate = condition is { } c
            ? !c.Full || (item.Kind & DeskItemKind.Armour) != 0
            : (item.Kind & (DeskItemKind.Armour | DeskItemKind.Meds | DeskItemKind.FoodDrink | DeskItemKind.Gun)) != 0;

        var noFlea = !flea.Enabled ? DeskNoFlea.FleaOff
            : item.NoFlea ? DeskNoFlea.NotAllowed
            : flea.FoundInRaidRequired && foundInRaid != true ? DeskNoFlea.NotFoundInRaid
            : item.LastLow is not > 0 ? DeskNoFlea.NoOffers
            : DeskNoFlea.None;
        double? fleaNet = null, fee = null;
        if (noFlea == DeskNoFlea.None)
        {
            fee = Fee(item.BasePrice, item.LastLow!.Value, count, flea, levels);
            fleaNet = item.LastLow!.Value * count - fee.Value;
        }
        var best = item.Traders.Count > 0 ? item.Traders.MaxBy(t => t.Price) : default;
        double? traderPrice = item.Traders.Count > 0 ? best.Price * count : null;

        var (source, worth, trader) = (fleaNet, traderPrice) switch
        {
            ({ } net, { } paid) when net >= paid => (DeskSource.Flea, net, (string?)null),
            (_, { } paid) => (DeskSource.Trader, paid, best.Trader),
            ({ } net, null) => (DeskSource.Flea, net, null),
            _ => (DeskSource.None, 0d, null),
        };
        // The cell shows what the flea lists at, not what a sale nets: the listing price at the last low.
        var full = source == DeskSource.Flea ? item.LastLow!.Value * count : Math.Max(0, worth);
        return new DeskItemValue(item.Id, source, Math.Round(full * scale), full, trader)
        {
            Worth = Math.Round(Math.Max(0, worth) * scale),
            FleaNet = fleaNet,
            Fee = fee,
            TraderPrice = traderPrice,
            NoFlea = noFlea,
            Estimate = estimate,
            Scale = scale,
        };
    }

    /// <summary>
    /// A cell's figure from its items' values: one item's, or for "one of N" the candidates' range (the least of them as its
    /// value, so a total counts at least that). "+" where the contents are unseen. The tint from the value per slot of the
    /// footprint (a slot's item by its catalogue footprint), the arrow from ±10 % in 48 h and the liquidity from the
    /// offers, for one item only. Null when no item has a figure (the secure container: no figure).
    /// </summary>
    public static DeskFigure? Figure(IReadOnlyList<(DeskItemFacts Facts, DeskItemValue Value)> items, int cells, bool unseen)
    {
        var priced = items.Where(i => i.Value.Source != DeskSource.None).ToList();
        if (priced.Count == 0)
            return null;
        var unsure = items.Count > 1;
        var low = priced.MinBy(i => i.Value.Value);
        var high = priced.MaxBy(i => i.Value.Value);
        var perSlot = low.Value.Worth / Math.Max(1, cells);
        var one = items.Count == 1 ? items[0] : default;
        var change = one.Facts?.Change48hPercent;
        return new DeskFigure(unsure ? DeskSource.None : low.Value.Source, low.Value.Value, high.Value.Value, unsure ? null : low.Value.Trader)
        {
            Estimate = items.Any(i => i.Value.Estimate),
            Plus = unseen,
            Unsure = unsure,
            Full = unsure ? low.Value.Full : one.Value.Full,
            PerSlot = perSlot,
            Worth = priced.Min(i => i.Value.Worth),
            Tint = Tint(perSlot),
            Arrow = unsure ? DeskArrow.None : Arrow(change),
            Change48h = unsure ? null : change,
            Liquidity = unsure ? DeskLiquidity.Unknown : Liquidity(one.Facts!.Offers),
            FleaLevel = unsure ? null : one.Facts!.FleaLevel,
            NoFlea = unsure ? DeskNoFlea.None : one.Value.NoFlea,
            FleaNet = unsure ? null : one.Value.FleaNet,
            Fee = unsure ? null : one.Value.Fee,
            TraderPrice = unsure ? null : one.Value.TraderPrice,
        };
    }

    /// <summary>The tint's step for a value per slot: 0 under 20k, 1 from 20k, 2 from 100k, 3 from 300k ₽.</summary>
    public static int Tint(double perSlot) => TintSteps.Count(step => perSlot >= step);

    /// <summary>↑ or ↓ where the 48 h change is 10 % or more either way.</summary>
    public static DeskArrow Arrow(double? changePercent) =>
        changePercent is >= ArrowFrom ? DeskArrow.Up : changePercent is <= -ArrowFrom ? DeskArrow.Down : DeskArrow.None;

    /// <summary>The liquidity dot: full from 50 offers, half from 10 to 49, a ring under 10.</summary>
    public static DeskLiquidity Liquidity(int? offers) => offers switch
    {
        null => DeskLiquidity.Unknown,
        >= 50 => DeskLiquidity.Full,
        >= 10 => DeskLiquidity.Half,
        _ => DeskLiquidity.Ring,
    };
}
