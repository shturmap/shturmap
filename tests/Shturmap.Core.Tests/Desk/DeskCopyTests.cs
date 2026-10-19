using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;

namespace Shturmap.Core.Tests.Desk;

// The list price a click on a desk cell copies (owner, 2026-10-19, from a panel: D2; owner, 2026-10-20: the click copies,
// the rule's control on the card; docs/DESIGN.md §4, *The stash desk*, *The click*; Core.Desk.DeskCopies): the last low
// matched, or 1–20 % under or over it, rounded so it stays under or over, scaled to a printed condition; and the guards.
// Made-up items.
public class DeskCopyTests
{
    private static DeskItemFacts Item(double? low, double basePrice, double trader = 0, bool noFlea = false, DeskItemKind kind = DeskItemKind.None) =>
        new("item")
        {
            LastLow = low,
            BasePrice = basePrice,
            NoFlea = noFlea,
            Kind = kind,
            FleaLevel = 20,
            Traders = trader > 0 ? [("therapist", trader), ("fence", trader / 3)] : [],
        };

    private static DeskCell Cell(DeskPrint? print = null, bool? fir = null, params string[] ids) =>
        new(0, 0, 1, 1, ids.Length == 0 ? ["item"] : ids) { Print = print, FoundInRaid = fir };

    private static readonly DeskFleaRules Flea = DeskFleaRules.Default;

    [Fact]
    public void The_rule_is_match_or_up_to_20_percent_under_or_over()
    {
        Assert.Equal(-3, DeskCopyRule.Default.Percent);
        Assert.Equal("under:3", DeskCopyRule.Default.Format());
        Assert.Equal(new DeskCopyRule(0), DeskCopyRule.Parse("match"));
        Assert.Equal(new DeskCopyRule(2), DeskCopyRule.Parse("over:2"));
        Assert.Equal(new DeskCopyRule(-20), DeskCopyRule.Parse("UNDER:40"));
        Assert.Equal(new DeskCopyRule(-1), DeskCopyRule.Parse("under:0"));
        Assert.Null(DeskCopyRule.Parse("sideways:3"));
        Assert.Null(DeskCopyRule.Parse("under"));
        Assert.Equal(DeskCopyRule.Default, DeskCopyRule.Of(null));
        Assert.Equal(DeskCopyRule.Default, DeskCopyRule.Of("nonsense"));
        // The card's − and +: a percent a step, through the match, never past 20.
        Assert.Equal("match", DeskCopyRule.Default.Step(3).Format());
        Assert.Equal("over:1", DeskCopyRule.Default.Step(4).Format());
        Assert.Equal("under:20", DeskCopyRule.Default.Step(-30).Format());
        Assert.Equal("over:20", new DeskCopyRule(19).Step(5).Format());
        foreach (var percent in Enumerable.Range(-20, 41))
            Assert.Equal(new DeskCopyRule(percent), DeskCopyRule.Parse(new DeskCopyRule(percent).Format()));
    }

    // The panel's LEDX: last low 1,000,000, 3 % under copies 970,000, which nets 873,000 after the 97,000 fee.
    [Fact]
    public void A_click_copies_the_last_low_moved_by_the_rule()
    {
        var ledx = Item(1_000_000, 970_000, 650_000);
        var copy = DeskCopies.Of(Cell(), ledx, Flea, DeskCopyRule.Default);
        Assert.Equal(DeskCopyState.Copy, copy.State);
        Assert.Equal(970_000, copy.Price);
        Assert.Equal(97_000, copy.Fee);
        Assert.Equal(873_000, copy.Net);
        Assert.Equal(1_000_000, copy.LastLow);
        Assert.Equal(650_000, copy.TraderPrice);
        Assert.Equal("therapist", copy.Trader);
        Assert.Equal(20, copy.FleaLevel);
        Assert.True(copy.Copies);
        Assert.Equal(1_000_000, DeskCopies.Of(Cell(), ledx, Flea, new DeskCopyRule(0)).Price);
        Assert.Equal(1_020_000, DeskCopies.Of(Cell(), ledx, Flea, new DeskCopyRule(2)).Price);
    }

    // Under the last low rounds down, so it stays under; over rounds up; a match is the last low itself.
    [Fact]
    public void The_price_is_whole_roubles_and_stays_on_its_side()
    {
        var item = Item(433_333, 400_000);
        Assert.Equal(420_333, DeskCopies.Of(Cell(), item, Flea, new DeskCopyRule(-3)).Price);
        Assert.Equal(446_333, DeskCopies.Of(Cell(), item, Flea, new DeskCopyRule(3)).Price);
        Assert.Equal(433_333, DeskCopies.Of(Cell(), item, Flea, new DeskCopyRule(0)).Price);
        Assert.Equal(1, DeskCopies.Of(Cell(), Item(1, 1), Flea, new DeskCopyRule(-20)).Price);
    }

    // *The cell*, Da: of a figure scaled to a printed condition the click copies the scaled price, an estimate ("~").
    [Fact]
    public void A_printed_condition_scales_the_price()
    {
        var grizzly = Item(41_000, 28_034, 14_000, kind: DeskItemKind.Meds);
        var copy = DeskCopies.Of(Cell(new DeskPrint(1469, 1800)), grizzly, Flea, DeskCopyRule.Default);
        Assert.Equal(DeskCopyState.Copy, copy.State);
        Assert.Equal(Math.Floor(41_000 * 1469 / 1800.0 * 0.97), copy.Price);
        Assert.True(copy.Estimate);
        Assert.Equal(new DeskPrint(1469, 1800), copy.Print);
        Assert.Equal(Math.Round(14_000 * 1469 / 1800.0), copy.TraderPrice);
        // Meds at a full print are no estimate.
        Assert.False(DeskCopies.Of(Cell(new DeskPrint(1800, 1800)), grizzly, Flea, DeskCopyRule.Default).Estimate);
    }

    // Guards (*The click*): nothing where the best trader pays more than the flea nets at that price (the panel's blue
    // keycard: Therapist 459,000, the flea 3 % under 433,000 nets about 338,000).
    [Fact]
    public void Nothing_is_copied_where_a_trader_pays_more()
    {
        var keycard = Item(433_000, 900_000, 459_000);
        var copy = DeskCopies.Of(Cell(), keycard, Flea, DeskCopyRule.Default);
        Assert.Equal(DeskCopyState.TraderPaysMore, copy.State);
        Assert.False(copy.Copies);
        Assert.Equal(0, copy.Price);
        Assert.Equal(459_000, copy.TraderPrice);
        Assert.True(copy.Net < 459_000);
    }

    // Nothing where the flea can't list it: noFlea, the found-in-raid rule with the mark unseen, no offers, the flea off.
    [Fact]
    public void Nothing_is_copied_where_the_flea_cant_list_it()
    {
        Assert.Equal(DeskNoFlea.NotAllowed, DeskCopies.Of(Cell(), Item(null, 780, 450, noFlea: true), Flea, DeskCopyRule.Default) is
            { State: DeskCopyState.NoFlea } c ? c.NoFlea : DeskNoFlea.None);
        var fir = Flea with { FoundInRaidRequired = true };
        Assert.Equal(DeskNoFlea.NotFoundInRaid, DeskCopies.Of(Cell(fir: false), Item(5_000, 4_000), fir, DeskCopyRule.Default).NoFlea);
        Assert.Equal(DeskCopyState.Copy, DeskCopies.Of(Cell(fir: true), Item(5_000, 4_000), fir, DeskCopyRule.Default).State);
        Assert.Equal(DeskNoFlea.NoOffers, DeskCopies.Of(Cell(), Item(null, 4_000, 100), Flea, DeskCopyRule.Default).NoFlea);
        Assert.Equal(DeskNoFlea.FleaOff, DeskCopies.Of(Cell(), Item(5_000, 4_000), Flea with { Enabled = false }, DeskCopyRule.Default).NoFlea);
        Assert.All(new[] { DeskCopies.Of(Cell(), Item(null, 780, noFlea: true), Flea, DeskCopyRule.Default) }, c => Assert.False(c.Copies));
    }

    // "One of N" copies nothing until a pick; a pick makes it the item; a cell nothing was said of, or an item the file
    // lacks, has no price.
    [Fact]
    public void A_question_mark_cell_copies_after_its_pick()
    {
        var unsure = DeskCopies.Of(Cell(ids: ["a", "b", "c"]), null, Flea, DeskCopyRule.Default);
        Assert.Equal(DeskCopyState.Unsure, unsure.State);
        Assert.Equal(3, unsure.Candidates);
        var picked = Cell(ids: ["a", "b", "c"]) with { Pick = "b" };
        Assert.Equal(DeskCopyState.Copy, DeskCopies.Of(picked, Item(5_000, 4_000) with { }, Flea, DeskCopyRule.Default).State);
        Assert.Equal(DeskCopyState.NoPrice, DeskCopies.Of(new DeskCell(0, 0, 1, 1, []), null, Flea, DeskCopyRule.Default).State);
        Assert.Equal(DeskCopyState.NoPrice, DeskCopies.Of(Cell(), null, Flea, DeskCopyRule.Default).State);
    }

    // Above the price that nets the most the fee grows faster than the price (about 51.5 times the base price at PvE's
    // rates of 2026-10-19): the click still copies, the plate warns (the panel's poster: 5,927,778 against a best 5,140,000).
    [Fact]
    public void Over_the_best_net_it_copies_and_warns()
    {
        var (best, bestNet) = DeskCopies.BestNet(100_000, Flea);
        Assert.InRange(best / 100_000, 51, 52);
        Assert.True(bestNet > best * 0.95 - DeskValues.Fee(100_000, best * 0.95, 1, Flea));
        Assert.True(bestNet > best * 1.05 - DeskValues.Fee(100_000, best * 1.05, 1, Flea));
        var poster = Item(6_111_111, 100_000, 50_000);
        var copy = DeskCopies.Of(Cell(), poster, Flea, DeskCopyRule.Default);
        Assert.Equal(DeskCopyState.OverBestNet, copy.State);
        Assert.True(copy.Copies);
        Assert.True(copy.Net < copy.BestNet);
        Assert.Equal((0d, 0d), DeskCopies.BestNet(0, Flea));
    }

    // The made-up desk holds a cell of each state, so one run of the replay pictures them all.
    [Fact]
    public void The_made_up_desk_holds_a_cell_of_each_state()
    {
        var view = DeskExample.View(1, GameMode.Pve, 1, null, DateTimeOffset.UnixEpoch);
        var copies = view.Sheet.Regions.SelectMany(r => r.Cells)
            .Select(c => DeskCopies.Of(c.Cell, c.Cell.ItemId is { } id ? view.FactsOf(id) : null, view.Flea, DeskCopyRule.Default)).ToList();
        Assert.Contains(copies, c => c.State == DeskCopyState.Copy && !c.Estimate);
        Assert.Contains(copies, c => c.State == DeskCopyState.Copy && c.Estimate && c.Scale < 1);
        Assert.Contains(copies, c => c.State == DeskCopyState.OverBestNet);
        Assert.Contains(copies, c => c.State == DeskCopyState.TraderPaysMore);
        Assert.Contains(copies, c => c.State == DeskCopyState.NoFlea);
        Assert.Contains(copies, c => c.State == DeskCopyState.Unsure);
    }
}
