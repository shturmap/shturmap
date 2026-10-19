namespace Shturmap.Core.Desk;

// The list price a click on a desk cell copies (owner, 2026-10-19, from a panel: D2; owner, 2026-10-19: "I don't think we
// need a visible copy button, clicking on the item suffices. But there should be a visual feedback and UI elements for
// setting what is copied clearly visible."; docs/DESIGN.md §4, "Screen anatomy", *The stash desk*, *The click*): the last
// low matched, or 1–20 % under or over it, scaled to a printed condition, and the guards that say when a click copies
// nothing or warns. Rules without side effects: the view writes the clipboard.

/// <summary>
/// The rule for what a click copies: the last low moved by <see cref="Percent"/> (−20 to +20; 0 matches it, −3 is 3 %
/// under). One state, shown on the card and in settings' row "Flea price to copy", kept in shturmap.db.
/// </summary>
public readonly record struct DeskCopyRule(int Percent)
{
    /// <summary>The most a rule goes under or over the last low, in percent.</summary>
    public const int Most = 20;

    /// <summary>The setting's key in shturmap.db: "match", "under:3", "over:2"; absent is 3 % under.</summary>
    public const string Setting = "desk.copyRule";

    /// <summary>3 % under the last low, unless changed.</summary>
    public static DeskCopyRule Default { get; } = new(-3);

    public bool Match => Percent == 0;

    public bool Under => Percent < 0;

    public bool Over => Percent > 0;

    /// <summary>How far under or over, in percent, without its sign.</summary>
    public int Amount => Math.Abs(Percent);

    /// <summary>The rule a step of <paramref name="by"/> percent away (− lowers the price, + raises it), within ±20.</summary>
    public DeskCopyRule Step(int by) => new(Math.Clamp(Percent + by, -Most, Most));

    /// <summary>"match", "under:3", "over:2", as shturmap.db keeps it.</summary>
    public string Format() =>
        Percent == 0 ? "match" : (Percent < 0 ? "under:" : "over:") + Math.Abs(Percent).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The rule a setting or a developer script's "desk rule" says ("match", "under:3", "over:2"); null for
    /// anything else. The amount is kept within 1 to 20.</summary>
    public static DeskCopyRule? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        text = text.Trim().ToLowerInvariant();
        if (text == "match")
            return new DeskCopyRule(0);
        var colon = text.IndexOf(':');
        if (colon <= 0 || !int.TryParse(text[(colon + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var amount))
            return null;
        amount = Math.Clamp(amount, 1, Most);
        return text[..colon] switch
        {
            "under" => new DeskCopyRule(-amount),
            "over" => new DeskCopyRule(amount),
            _ => null,
        };
    }

    /// <summary>The rule a setting says, or the default when it says none.</summary>
    public static DeskCopyRule Of(string? setting) => Parse(setting) ?? Default;
}

/// <summary>What a click on a cell does with its price.</summary>
public enum DeskCopyState
{
    /// <summary>It copies the list price.</summary>
    Copy,

    /// <summary>It copies the list price, but that is above the most a sale can net (the fee's curve): the card's plate
    /// carries the status bar's gold light.</summary>
    OverBestNet,

    /// <summary>The flea nets less at the list price than the best trader pays: nothing is copied.</summary>
    TraderPaysMore,

    /// <summary>No flea price: the item can't be listed, needs the found-in-raid mark, has no offers, or the flea is off.
    /// Nothing is copied.</summary>
    NoFlea,

    /// <summary>"One of N": which item it is isn't known until the player picks it on the card. Nothing is copied.</summary>
    Unsure,

    /// <summary>Nothing was read of the cell, or the item file has no such item: nothing is copied.</summary>
    NoPrice,
}

/// <summary>A click's copy for one cell: the price per item to list at and the net it brings after the fee, or why it
/// copies nothing.</summary>
public sealed record DeskCopy(DeskCopyState State)
{
    /// <summary>The list price per item, whole roubles: the last low moved by the rule, scaled to a printed condition;
    /// 0 when nothing is copied.</summary>
    public double Price { get; init; }

    /// <summary>The last low per item (at full condition).</summary>
    public double LastLow { get; init; }

    /// <summary>The flea's fee for one item at the list price, and what the sale nets after it.</summary>
    public double Fee { get; init; }

    public double Net { get; init; }

    /// <summary>The price that nets the most after the fee, and that net (the fee's curve: about 51 times the base price
    /// at PvE's rates of 2026-10-19).</summary>
    public double BestPrice { get; init; }

    public double BestNet { get; init; }

    /// <summary>The best trader's price per item (scaled as the price is), and the trader's id, or null when none buys it.</summary>
    public double? TraderPrice { get; init; }

    public string? Trader { get; init; }

    /// <summary>"~": an estimate from condition; <see cref="Print"/> is what it was scaled to.</summary>
    public bool Estimate { get; init; }

    public DeskPrint? Print { get; init; }

    /// <summary>The share of a printed condition the price was scaled by (1 when none).</summary>
    public double Scale { get; init; } = 1;

    /// <summary>Why there is no flea price (<see cref="DeskCopyState.NoFlea"/>).</summary>
    public DeskNoFlea NoFlea { get; init; }

    /// <summary>The item's flea level ("flea from level 35"); compared with nothing (the player's level isn't read).</summary>
    public int? FleaLevel { get; init; }

    /// <summary>For "one of N", how many items it may be.</summary>
    public int Candidates { get; init; }

    /// <summary>Whether a click copies anything.</summary>
    public bool Copies => State is DeskCopyState.Copy or DeskCopyState.OverBestNet;
}

public static class DeskCopies
{
    /// <summary>
    /// What a click on <paramref name="cell"/> copies by <paramref name="rule"/>: the last low moved by the rule (under the
    /// last low rounded down to the rouble, over it rounded up, a match as it is), scaled to the condition the cell prints
    /// (*The cell*, Da: a Grizzly at 1469/1800 copies 1469/1800 of the price); per item, as the flea's price field takes
    /// it. Guards (*The click*): nothing for "one of N" until a pick; nothing where the flea can't list it (noFlea, the
    /// found-in-raid rule, no offers, the flea off); nothing where the flea nets less at that price than the best trader
    /// pays; a warning above the price that nets the most.
    /// </summary>
    public static DeskCopy Of(DeskCell cell, DeskItemFacts? facts, DeskFleaRules flea, DeskCopyRule rule, DeskFeeLevels levels = default)
    {
        if (cell.ItemId is null)
            return cell.Ids.Count > 1 ? new DeskCopy(DeskCopyState.Unsure) { Candidates = cell.Ids.Count } : new DeskCopy(DeskCopyState.NoPrice);
        if (facts is null)
            return new DeskCopy(DeskCopyState.NoPrice);
        // The cell's value per item: its scale and "~", its flea rule, the best trader.
        var one = DeskValues.Of(facts, 1, cell.Print, cell.FoundInRaid, flea, levels);
        var scale = one.Scale;
        double? trader = one.TraderPrice is { } paid ? Math.Round(paid * scale, MidpointRounding.AwayFromZero) : null;
        var best = facts.Traders.Count > 0 ? facts.Traders.MaxBy(t => t.Price).Trader : null;
        var copy = new DeskCopy(DeskCopyState.Copy)
        {
            LastLow = facts.LastLow ?? 0,
            TraderPrice = trader,
            Trader = best,
            Estimate = one.Estimate,
            Print = one.Scale < 1 || one.Estimate ? cell.Print : null,
            Scale = scale,
            NoFlea = one.NoFlea,
            FleaLevel = facts.FleaLevel,
        };
        if (one.NoFlea != DeskNoFlea.None)
            return copy with { State = DeskCopyState.NoFlea };

        var basePrice = facts.BasePrice * scale;
        var price = Rounded(facts.LastLow!.Value * scale * (1 + rule.Percent / 100.0), rule);
        var fee = DeskValues.Fee(basePrice, price, 1, flea, levels);
        var net = price - fee;
        var (bestPrice, bestNet) = BestNet(basePrice, flea, levels);
        copy = copy with { Price = price, Fee = fee, Net = net, BestPrice = bestPrice, BestNet = bestNet };
        if (trader is { } pays && pays > net)
            return copy with { State = DeskCopyState.TraderPaysMore, Price = 0 };
        // Above the price that nets the most: a higher price nets less (the fee's curve). A rouble's rounding is no warning.
        return bestPrice > 0 && price > bestPrice + 1 ? copy with { State = DeskCopyState.OverBestNet } : copy;
    }

    // Whole roubles: under the last low rounded down (it stays under), over it rounded up, a match as it is; never under 1.
    private static double Rounded(double price, DeskCopyRule rule) => Math.Max(1, rule.Under ? Math.Floor(price + 1e-6)
        : rule.Over ? Math.Ceiling(price - 1e-6)
        : Math.Round(price, MidpointRounding.AwayFromZero));

    /// <summary>
    /// The list price that nets the most after the fee for an item of <paramref name="basePrice"/>, and that net: past it
    /// the fee grows faster than the price (tarkov.dev's formula, <see cref="DeskValues.Fee"/>). Found on the curve between
    /// a tenth and 300 times the base price, then narrowed to the rouble. (0, 0) without a base price.
    /// </summary>
    public static (double Price, double Net) BestNet(double basePrice, DeskFleaRules flea, DeskFeeLevels levels = default)
    {
        if (basePrice <= 0)
            return (0, 0);
        double Net(double p) => p - DeskValues.Fee(basePrice, p, 1, flea, levels);
        // A coarse walk in log steps, then a golden-section search around the best step.
        double lo = Math.Log(basePrice * 0.1), hi = Math.Log(basePrice * 300);
        const int steps = 600;
        var bestAt = lo;
        var best = double.MinValue;
        for (var i = 0; i <= steps; i++)
        {
            var x = lo + (hi - lo) * i / steps;
            var n = Net(Math.Exp(x));
            if (n > best)
                (best, bestAt) = (n, x);
        }
        var step = (hi - lo) / steps;
        double a = bestAt - step, b = bestAt + step;
        var g = (Math.Sqrt(5) - 1) / 2;
        for (var i = 0; i < 80; i++)
        {
            double c = b - g * (b - a), d = a + g * (b - a);
            if (Net(Math.Exp(c)) > Net(Math.Exp(d)))
                b = d;
            else
                a = c;
        }
        var price = Math.Round(Math.Exp((a + b) / 2), MidpointRounding.AwayFromZero);
        return (price, Net(price));
    }
}
