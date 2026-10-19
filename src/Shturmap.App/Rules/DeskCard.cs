using Shturmap.Core;
using Shturmap.Core.Desk;

namespace Shturmap.App.Rules;

/// <summary>What the card's plate says of a click: what it copies, a warning with the gold light, or that it copies nothing.</summary>
public enum DeskPlate
{
    Copies,
    Warning,
    Nothing,
    Pick,
}

/// <summary>The history band's state: being fetched (a quiet "…"), drawn, or not at hand.</summary>
public enum DeskBandState
{
    Loading,
    Shown,
    None,
}

/// <summary>One KEEP line: its notch and words ("Private Clinic ×1 · now · FIR").</summary>
public sealed record DeskCardKeepLine(DeskNotch Notch, string Text);

/// <summary>One trader's line: portrait, name and what they pay for one.</summary>
public sealed record DeskCardTrader(string TraderId, string Name, string Price);

/// <summary>A craft or barter that gives the item, as the item card words it ("Mechanic LL2 barter · 2× Bolts").</summary>
public sealed record DeskCardSource(string? TraderId, string Text, bool Craft);

/// <summary>A "?" cell's candidate: its name and figure, a click picks it.</summary>
public sealed record DeskCardCandidate(string ItemId, string Name, string Figure, DeskSource Source);

/// <summary>What the card needs besides the cell: the item file's facts, the flea's rules, the copy rule, the names, the
/// prices' age and the time now.</summary>
public sealed record DeskCardContext(Func<string, DeskItemFacts?> Facts, DeskFleaRules Flea, DeskCopyRule Rule, Func<string, string> ItemName,
    Func<string, string> TraderName, Func<string, string> QuestName, Func<string, string> StationName, DateTimeOffset? PricesAt, DateTimeOffset Now);

/// <summary>
/// Everything the stash desk's card says, in its order (owner, 2026-10-19, from a panel: C1; owner, 2026-10-20: the
/// sparkline, the 24 h bar, every trader, the keep lines and the copy; docs/DESIGN.md §4, *The stash desk*, *The card*):
/// decision first (the item, its offers and flea level, the gold figure with its 7-day change, the flea's net with the fee
/// and the 48 h change, the plate with what a click copies by the rule, the condition, KEEP), then the 30 days' band, the
/// day's bar, the traders, the crafts and barters, and the prices' age. A "?" cell's card is its candidates instead.
/// </summary>
public sealed record DeskCardWords
{
    public required string ItemId { get; init; }

    /// <summary>The slot's name in muted capitals over the item's ("HEADWEAR"), or null for a grid's cell.</summary>
    public string? Slot { get; init; }

    public required string Name { get; init; }

    public DeskLiquidity Liquidity { get; init; }

    /// <summary>"40 OFFERS · FLEA FROM LEVEL 35", "NOT ON THE FLEA".</summary>
    public string Offers { get; init; } = "";

    /// <summary>The cell's figure in full, "~873,000 ₽+"; null with no price.</summary>
    public string? Figure { get; init; }

    /// <summary>"↑ +1.7% · 7 D" from the history; "…" while it is fetched; null with no history.</summary>
    public string? SevenDays { get; init; }

    /// <summary>"Flea net 873,000 ₽ · fee 97,000 ₽", and its 48 h change, "↓ −10.3% · 48 H"; null where the flea has no figure.</summary>
    public string? FleaNet { get; init; }

    public string? TwoDays { get; init; }

    /// <summary>What a click copies, by the rule.</summary>
    public required DeskCopy Copy { get; init; }

    public DeskPlate Plate { get; init; }

    /// <summary>The rule's words between − and +: "3% UNDER", "THE LAST LOW", "2% OVER".</summary>
    public string Rule { get; init; } = "";

    /// <summary>The plate's line: "Copies 970,000 ₽ · nets 873,000 ₽ after the fee", or why nothing.</summary>
    public string PlateLine { get; init; } = "";

    /// <summary>The plate's second line, muted: "Last low 1,000,000 ₽ · Therapist pays 494,700 ₽".</summary>
    public string? PlateReason { get; init; }

    /// <summary>"Scaled to 1469/1800 · full 37,386 ₽", the "~" note, or "Contents unseen: …".</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    public IReadOnlyList<DeskCardKeepLine> Keep { get; init; } = [];

    public DeskBandState Band { get; init; }

    /// <summary>The 30 days' points (average and lowest offer), for the band.</summary>
    public IReadOnlyList<DeskPricePoint> BandPoints { get; init; } = [];

    /// <summary>"HIGH 1.05 M · LOW 0.82 M, 13 DAYS AGO".</summary>
    public string? HighLow { get; init; }

    public DeskDayRange? Day { get; init; }

    /// <summary>"LAST LOW 1,000,000 · AVG 983,351", over the day's bar.</summary>
    public string? DayMarks { get; init; }

    /// <summary>The bar's ends' words: its low and its end.</summary>
    public string? DayLow { get; init; }

    public string? DayHigh { get; init; }

    /// <summary>"The high, 9,999,999 ₽, is past the bar: one offer, likely an outlier."</summary>
    public string? Outlier { get; init; }

    public IReadOnlyList<DeskCardTrader> Traders { get; init; } = [];

    public IReadOnlyList<DeskCardSource> Sources { get; init; } = [];

    /// <summary>"Prices 4 min old · tarkov.dev".</summary>
    public string? Age { get; init; }

    /// <summary>A "?" cell's candidates, best figure first; empty for one item.</summary>
    public IReadOnlyList<DeskCardCandidate> Candidates { get; init; } = [];
}

public static class DeskCards
{
    /// <summary>The card of <paramref name="sheet"/> in <paramref name="region"/>.</summary>
    /// <param name="history">The item's price history, null while it is fetched (<paramref name="historyLoading"/>) or when
    /// it couldn't be had.</param>
    /// <param name="sources">The crafts and barters that give the item, as the item card words them.</param>
    public static DeskCardWords Of(DeskRegion region, DeskCellSheet sheet, DeskCardContext context, DeskHistory? history = null, bool historyLoading = false,
        IReadOnlyList<DeskCardSource>? sources = null)
    {
        var cell = sheet.Cell;
        var age = Age(context.PricesAt, context.Now);
        var copy = DeskCopies.Of(cell, cell.ItemId is { } one ? context.Facts(one) : null, context.Flea, context.Rule);
        if (cell.ItemId is not { } id)
        {
            // "One of N": its candidates, the best figure first; a click on one picks it.
            var candidates = cell.Ids.Select(c => (Id: c, Facts: context.Facts(c)))
                .Select(c => (c.Id, Value: c.Facts is null ? null : DeskValues.Of(c.Facts, cell.Count, cell.Print, cell.FoundInRaid, context.Flea)))
                .OrderByDescending(c => c.Value?.Value ?? -1)
                .Select(c => new DeskCardCandidate(c.Id, context.ItemName(c.Id), c.Value is { Source: not DeskSource.None } v
                    ? DeskNumbers.Figure(v.Value, v.Value, false, v.Estimate, false) : "—", c.Value?.Source ?? DeskSource.None))
                .ToList();
            return new DeskCardWords
            {
                ItemId = cell.Ids.FirstOrDefault() ?? "",
                Slot = SlotName(cell.Slot),
                Name = RuleTexts.DeskCardOneOf(count: cell.Ids.Count),
                Copy = copy,
                Plate = DeskPlate.Pick,
                PlateLine = RuleTexts.DeskCardPickNote,
                Candidates = candidates,
                Age = age,
                Band = DeskBandState.None,
            };
        }

        var facts = context.Facts(id);
        var figure = sheet.Figure;
        var offers = new List<string>();
        if (facts is { NoFlea: true })
            offers.Add(RuleTexts.DeskCardNotOnFlea);
        else
        {
            if (facts?.Offers is { } count)
                offers.Add(RuleTexts.DeskCardOffers(count: count));
            if (facts?.FleaLevel is { } level and > 0)
                offers.Add(RuleTexts.DeskCardFleaLevel(level: level));
        }
        var (plate, line) = PlateOf(copy, context);
        var notes = new List<string>();
        if (figure is { Estimate: true } && cell.Print is { } print && copy.Scale < 1)
            notes.Add(RuleTexts.DeskCardScaled(print: print.ToString(), full: Roubles(figure.Full)));
        else if (figure is { Estimate: true })
            notes.Add(RuleTexts.DeskCardEstimate);
        if (sheet.Unseen)
            notes.Add(RuleTexts.DeskCardUnseen);
        var reason = new List<string>();
        if (copy.LastLow > 0 && copy.NoFlea == DeskNoFlea.None)
            reason.Add(RuleTexts.DeskCardLastLow(price: Roubles(copy.LastLow)));
        // The best trader, unless the plate's line already says they pay more.
        if (copy.Trader is { } trader && copy.TraderPrice is { } paid && copy.State != DeskCopyState.TraderPaysMore)
            reason.Add(RuleTexts.DeskCardTraderPays(trader: context.TraderName(trader), price: Roubles(paid)));

        var day = facts is null ? null : DeskDayRange.Of(facts);
        var range = history?.Range();
        return new DeskCardWords
        {
            ItemId = id,
            Slot = SlotName(cell.Slot),
            Name = context.ItemName(id),
            Liquidity = figure?.Liquidity ?? DeskLiquidity.Unknown,
            Offers = string.Join(" · ", offers),
            Figure = figure is null ? null : (figure.Estimate ? "~" : "") + Roubles(figure.Value) + (figure.Plus ? "+" : ""),
            SevenDays = historyLoading ? RuleTexts.DeskCardHistoryLoading
                : history?.Change7d is { } seven ? RuleTexts.DeskCardSevenDays(change: Change(seven)) : null,
            FleaNet = figure?.FleaNet is { } net && figure.Fee is { } fee ? RuleTexts.DeskCardFleaNet(net: Roubles(net * copy.Scale), fee: Roubles(fee * copy.Scale)) : null,
            TwoDays = facts?.Change48hPercent is { } two ? RuleTexts.DeskCardTwoDays(change: Change(two)) : null,
            Copy = copy,
            Plate = plate,
            Rule = RuleWords(context.Rule),
            PlateLine = line,
            PlateReason = reason.Count > 0 ? string.Join(" · ", reason) : null,
            Notes = notes,
            Keep = sheet.Needs.Select(n => KeepLine(n, context)).ToList(),
            Band = historyLoading ? DeskBandState.Loading : history is { Points.Count: > 1 } ? DeskBandState.Shown : DeskBandState.None,
            BandPoints = history?.Recent(DeskHistory.Band) ?? [],
            HighLow = range is { } r ? RuleTexts.DeskCardHighLow(high: DeskNumbers.Short(r.High), low: DeskNumbers.Short(r.Low), days: r.LowDaysAgo) : null,
            Day = day,
            DayMarks = day is null ? null : RuleTexts.DeskCardDayMarks(last: day.LastLow is { } last ? Whole(last) : "—", avg: day.Avg is { } avg ? Whole(avg) : "—"),
            DayLow = day is null ? null : Whole(day.Low),
            DayHigh = day is null ? null : Whole(day.BarHigh),
            Outlier = day is { Outlier: true } ? RuleTexts.DeskCardOutlier(high: Roubles(day.High)) : null,
            Traders = (facts?.Traders ?? []).OrderByDescending(t => t.Price)
                .Select(t => new DeskCardTrader(t.Trader, context.TraderName(t.Trader), Roubles(t.Price))).ToList(),
            Sources = sources ?? [],
            Age = age,
        };
    }

    /// <summary>The plate's state and line for a click's copy (*The click*, the guards): what it copies and nets; above the
    /// best net, the gold light and the warning; a trader paying more, the flea unable to list it, or no price: nothing
    /// copied, and why.</summary>
    public static (DeskPlate Plate, string Line) PlateOf(DeskCopy copy, DeskCardContext context) => copy.State switch
    {
        DeskCopyState.Copy => (DeskPlate.Copies, RuleTexts.DeskCardCopies(price: Price(copy), net: Roubles(copy.Net))),
        DeskCopyState.OverBestNet => (DeskPlate.Warning, RuleTexts.DeskCardOverBest(price: Price(copy), best: Roubles(copy.BestPrice),
            bestNet: Roubles(copy.BestNet), net: Roubles(copy.Net))),
        DeskCopyState.TraderPaysMore => (DeskPlate.Nothing, RuleTexts.DeskCardTraderPaysMore(trader: context.TraderName(copy.Trader ?? ""),
            price: Roubles(copy.TraderPrice ?? 0))),
        DeskCopyState.NoFlea => (DeskPlate.Nothing, copy.NoFlea switch
        {
            DeskNoFlea.NotFoundInRaid => RuleTexts.DeskCardNoFleaFir,
            DeskNoFlea.NoOffers => RuleTexts.DeskCardNoOffers,
            DeskNoFlea.FleaOff => RuleTexts.DeskCardFleaOff,
            _ => RuleTexts.DeskCardNoFlea,
        }),
        DeskCopyState.Unsure => (DeskPlate.Pick, RuleTexts.DeskCardPickNote),
        _ => (DeskPlate.Nothing, RuleTexts.DeskCardNoPrice),
    };

    /// <summary>The one-line notice after a click (*The click*): "970,000 ₽ copied · 3% under the last low", with ", scaled
    /// to 1469/1800" or ", over the best net"; or why nothing was copied.</summary>
    public static string Notice(DeskCopy copy, DeskCopyRule rule, Func<string, string> traderName)
    {
        var said = rule.Match ? RuleTexts.DeskCopiedMatch : rule.Under ? RuleTexts.DeskCopiedUnder(amount: rule.Amount) : RuleTexts.DeskCopiedOver(amount: rule.Amount);
        if (copy.Scale < 1 && copy.Print is { } print)
            said = RuleTexts.DeskCopiedScaled(rule: said, print: print.ToString());
        if (copy.State == DeskCopyState.OverBestNet)
            said = RuleTexts.DeskCopiedOverBest(rule: said);
        return copy.State switch
        {
            DeskCopyState.Copy or DeskCopyState.OverBestNet => RuleTexts.DeskCopied(price: Roubles(copy.Price), rule: said),
            DeskCopyState.TraderPaysMore => RuleTexts.DeskNothingTrader(trader: traderName(copy.Trader ?? ""), price: Roubles(copy.TraderPrice ?? 0)),
            DeskCopyState.NoFlea => RuleTexts.DeskNothingNoFlea,
            DeskCopyState.Unsure => RuleTexts.DeskNothingPick(count: copy.Candidates),
            _ => RuleTexts.DeskNothingPrice,
        };
    }

    /// <summary>What goes on the clipboard: the price's digits alone ("970000"), as the flea's price field takes a paste.</summary>
    public static string Clipboard(DeskCopy copy) => copy.Price.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The rule between − and +: "3% UNDER", "THE LAST LOW", "2% OVER".</summary>
    public static string RuleWords(DeskCopyRule rule) =>
        rule.Match ? RuleTexts.DeskRuleMatch : rule.Under ? RuleTexts.DeskRuleUnder(amount: rule.Amount) : RuleTexts.DeskRuleOver(amount: rule.Amount);

    /// <summary>A KEEP line: "Private Clinic ×1 · now · FIR", "Medstation 3 ×1 · a hideout module".</summary>
    public static DeskCardKeepLine KeepLine(DeskNeed need, DeskCardContext context)
    {
        var line = need.StationLevel is { } level
            ? RuleTexts.DeskCardKeepStation(station: context.StationName(need.For), level: level, count: need.Count)
            : RuleTexts.DeskCardKeepQuest(quest: context.QuestName(need.For), count: need.Count, when: need.Level switch
            {
                DeskKeepLevel.Now => RuleTexts.DeskCardNow,
                DeskKeepLevel.Next => RuleTexts.DeskCardNext,
                _ => RuleTexts.DeskCardLater,
            });
        return new DeskCardKeepLine(DeskCells.NotchOf(need.Level), need.FoundInRaid ? RuleTexts.DeskCardFir(line: line) : line);
    }

    /// <summary>A change on the card: the arrow (↑ above 0, ↓ below), its sign and a tenth of a percent ("↑ +1.7%",
    /// "↓ −10.3%", "0%"). Never a colour: up and down are arrows and signs ("Visual language").</summary>
    public static string Change(double percent)
    {
        var tenth = Math.Round(percent, 1, MidpointRounding.AwayFromZero);
        var sign = tenth > 0 ? "+" : tenth < 0 ? "−" : "";
        var text = RuleTexts.DeskChange(change: sign + Math.Abs(tenth).ToString("0.#", UiLanguage.Culture));
        return tenth > 0 ? "↑ " + text : tenth < 0 ? "↓ " + text : text;
    }

    /// <summary>"Prices 4 min old · tarkov.dev", in hours from two on; null while the prices haven't loaded.</summary>
    public static string? Age(DateTimeOffset? pricesAt, DateTimeOffset now)
    {
        if (pricesAt is not { } at)
            return null;
        var minutes = (int)Math.Max(0, Math.Floor((now - at).TotalMinutes));
        return minutes < 120 ? RuleTexts.DeskCardAge(minutes: minutes) : RuleTexts.DeskCardAgeHours(hours: minutes / 60);
    }

    /// <summary>A loadout slot's name as the card heads its item ("HEADWEAR"), or a middle region's own container's; null for
    /// a grid's cell.</summary>
    public static string? SlotName(string? slot) => slot switch
    {
        null => null,
        "earpiece" => RuleTexts.DeskSlotEarpiece,
        "headwear" => RuleTexts.DeskSlotHeadwear,
        "facecover" => RuleTexts.DeskSlotFaceCover,
        "armband" => RuleTexts.DeskSlotArmband,
        "bodyarmor" => RuleTexts.DeskSlotBodyArmor,
        "eyewear" => RuleTexts.DeskSlotEyewear,
        "dogtag" => RuleTexts.DeskSlotDogtag,
        "onsling" => RuleTexts.DeskSlotOnSling,
        "holster" => RuleTexts.DeskSlotHolster,
        "onback" => RuleTexts.DeskSlotOnBack,
        "sheath" => RuleTexts.DeskSlotSheath,
        "rig" => RuleTexts.DeskRig,
        "backpack" => RuleTexts.DeskBackpack,
        "pouch" => RuleTexts.DeskPouch,
        _ => null,
    };

    /// <summary>A full price in roubles, the language's thousands separator: "970,000 ₽" ("970.000 ₽").</summary>
    public static string Roubles(double amount) => RuleTexts.DeskCardRoubles(amount: Whole(amount));

    private static string Price(DeskCopy copy) => (copy.Estimate ? "~" : "") + Roubles(copy.Price);

    private static string Whole(double amount) => Math.Round(amount, MidpointRounding.AwayFromZero).ToString("N0", UiLanguage.Culture);
}
