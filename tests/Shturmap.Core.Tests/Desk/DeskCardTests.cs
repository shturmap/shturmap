using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;

namespace Shturmap.Core.Tests.Desk;

// The stash desk's card (owner, 2026-10-19, from a panel: C1; owner, 2026-10-20; docs/DESIGN.md §4, *The stash desk*, *The
// card* and *The click*; Rules.DeskCards), its words and figures from the made-up desk (Rules.DeskExample): decision first,
// the plate in each of the copy's states, KEEP in progress order, the band, the day's bar, the traders, the age; the
// notice a click gives. English.
[Collection("UiLanguage")]
public class DeskCardTests
{
    private const string Nb = " ";

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 14, 6, 0, TimeSpan.Zero);

    private static readonly DeskView View = DeskExample.View(1, GameMode.Pve, 1, null, Now.AddMinutes(-4));

    private static readonly Dictionary<string, string> Names = new()
    {
        ["5c0be5fc86f774467a116593"] = "Private Clinic",
        ["60e71c48c1bfa3050473b8e5"] = "Crisis",
        ["5a68661a86f774500f48afb0"] = "Health Care Privacy - Part 1",
        ["5d484fcd654e7668ec2ec322"] = "Medstation",
        ["5d484fdf654e7600691aadf8"] = "Intelligence Center",
        ["54cb57776803fa99248b456e"] = "Therapist",
        ["579dc571d53a0658a154fbec"] = "Fence",
        ["54cb50c76803fa8b248b4571"] = "Prapor",
    };

    private static string Name(string id) => Names.GetValueOrDefault(id, id);

    private static DeskCardContext Context(DeskCopyRule? rule = null) =>
        new(View.FactsOf, View.Flea, rule ?? DeskCopyRule.Default, id => "item " + id[..4], Name, Name, Name, View.PricesAt, Now);

    private static (DeskRegion Region, DeskCellSheet Cell) At(string region, int i, int j, string? slot = null)
    {
        var sheet = View.Sheet.Regions.Single(r => r.Region.Key == region);
        return (sheet.Region, sheet.Cells.Single(c => slot is not null ? c.Cell.Slot == slot : !c.Cell.IsSlot && c.Cell.I == i && c.Cell.J == j));
    }

    private static DeskCardWords Card(string region, int i, int j, DeskHistory? history = null, bool loading = false, DeskCopyRule? rule = null)
    {
        var (r, c) = At(region, i, j);
        return DeskCards.Of(r, c, Context(rule), history, loading);
    }

    // The stash's LEDX: its offers and flea level, the figure (the flea's net at the last low), the plate: 3 % under copies
    // 970,000, which nets 873,000; the last low and the best trader under it; KEEP in progress order (now, a hideout
    // module's, later) with FIR; the day's bar with its outlier said; every trader, the best first; the prices' age.
    [Fact]
    public void Decision_first_then_the_history_and_the_traders()
    {
        var ledx = Card("stash", 8, 0, DeskExample.History("5c0530ee86f774697952d952", Now));
        Assert.Null(ledx.Slot);
        Assert.Equal("item 5c05", ledx.Name);
        Assert.Equal(DeskLiquidity.Full, ledx.Liquidity);
        Assert.Equal("60 OFFERS · FLEA FROM LEVEL 15", ledx.Offers);
        Assert.Equal($"901,728{Nb}₽", ledx.Figure);
        Assert.Equal(DeskPlate.Copies, ledx.Plate);
        Assert.Equal("3% UNDER", ledx.Rule);
        Assert.Equal($"Copies 970,000{Nb}₽ · nets 873,000{Nb}₽ after the fee", ledx.PlateLine);
        Assert.Equal($"Last low 1,000,000{Nb}₽ · Therapist pays 650,000{Nb}₽", ledx.PlateReason);
        Assert.Equal($"Flea net 901,728{Nb}₽ · fee 98,272{Nb}₽", ledx.FleaNet);
        Assert.Equal($"↓{Nb}−10.4% · 48{Nb}H", ledx.TwoDays);
        Assert.Equal(["Private Clinic ×1 · now · FIR", "Medstation 3 ×1 · a hideout module", "Crisis ×2 · later · FIR"], ledx.Keep.Select(k => k.Text));
        Assert.Equal([DeskNotch.Now, DeskNotch.Hideout, DeskNotch.Later], ledx.Keep.Select(k => k.Notch));
        Assert.Equal(DeskBandState.Shown, ledx.Band);
        Assert.Equal(30 * 12, ledx.BandPoints.Count);
        Assert.NotNull(ledx.SevenDays);
        Assert.EndsWith($" · 7{Nb}D", ledx.SevenDays);
        Assert.StartsWith("HIGH ", ledx.HighLow);
        Assert.Equal($"LAST LOW 1,000,000 · AVG 980,000", ledx.DayMarks);
        Assert.Equal("820,000", ledx.DayLow);
        Assert.Equal("1,100,000", ledx.DayHigh);
        Assert.Equal($"The high, 9,999,999{Nb}₽, is past the bar: one offer, likely an outlier.", ledx.Outlier);
        Assert.Equal(["Therapist", "Fence"], ledx.Traders.Select(t => t.Name));
        Assert.Equal($"650,000{Nb}₽", ledx.Traders[0].Price);
        Assert.Equal("Prices 4 min old · tarkov.dev", ledx.Age);
        Assert.Empty(ledx.Candidates);
    }

    // The plate's guards, each said where the click's result is: a trader pays more (nothing), can't be listed (nothing),
    // over the best net (the gold light), a scaled condition ("~", the condition line).
    [Fact]
    public void The_plate_says_each_guard()
    {
        var keycard = Card("pockets", 0, 0);
        Assert.Equal(DeskPlate.Nothing, keycard.Plate);
        Assert.Equal($"Therapist pays more · 459,000{Nb}₽. A click copies nothing.", keycard.PlateLine);
        var ammo = Card("rig", 5, 1);
        Assert.Equal(DeskPlate.Nothing, ammo.Plate);
        Assert.Equal("Can't be listed on the flea. A click copies nothing.", ammo.PlateLine);
        Assert.Equal("NOT ON THE FLEA", ammo.Offers);
        var key = Card("stash", 5, 2);
        Assert.Equal(DeskPlate.Warning, key.Plate);
        Assert.StartsWith($"Copies 1,832,221{Nb}₽, over the best net: at ", key.PlateLine);
        var grizzly = Card("backpack", 4, 1);
        Assert.Equal(DeskPlate.Copies, grizzly.Plate);
        Assert.StartsWith("Copies ~", grizzly.PlateLine);
        Assert.Contains("Scaled to 1469/1800 · full ", grizzly.Notes[0]);
        // A container with its contents unseen says so.
        Assert.Contains("Contents unseen: open it in the game, then a screenshot", Card("stash", 0, 0).Notes);
        // The rule's words follow the rule.
        Assert.Equal("THE LAST LOW", Card("stash", 8, 0, rule: new DeskCopyRule(0)).Rule);
        Assert.Equal("2% OVER", Card("stash", 8, 0, rule: new DeskCopyRule(2)).Rule);
    }

    // A "?" cell's card: its candidates, the best figure first, to pick from; the plate copies nothing.
    [Fact]
    public void A_question_mark_cards_candidates_are_to_pick()
    {
        var unsure = Card("stash", 4, 1);
        Assert.Equal(DeskPlate.Pick, unsure.Plate);
        Assert.Equal("One of 3 items", unsure.Name);
        Assert.Equal(3, unsure.Candidates.Count);
        Assert.Equal("Pick the one it is: the pick stays for this cell, and a click copies its price.", unsure.PlateLine);
        // The blue keycard first (Therapist pays 459k), then the violet, then the yellow.
        Assert.Equal(["5c1d0c5f86f7744bb2683cf0", "5c1e495a86f7743109743dfb", "5c1d0d6d86f7744bb2683e1f"], unsure.Candidates.Select(c => c.ItemId));
        Assert.Equal("459k", unsure.Candidates[0].Figure);
        // A slot's card names the slot.
        var (loadout, helmet) = At("loadout", 0, 0, "headwear");
        Assert.Equal("HEADWEAR", DeskCards.Of(loadout, helmet, Context()).Slot);
    }

    // While the history comes: "…" for the 7-day change and the band; without one: no change, "no history".
    [Fact]
    public void The_band_waits_quietly_for_its_history()
    {
        var waiting = Card("stash", 8, 0, loading: true);
        Assert.Equal("…", waiting.SevenDays);
        Assert.Equal(DeskBandState.Loading, waiting.Band);
        var none = Card("stash", 8, 0);
        Assert.Null(none.SevenDays);
        Assert.Equal(DeskBandState.None, none.Band);
    }

    // The click's one-line notice: what was copied and by which rule, or why nothing was.
    [Fact]
    public void A_click_says_what_it_copied_or_why_not()
    {
        string Notice(string region, int i, int j, DeskCopyRule rule)
        {
            var (_, c) = At(region, i, j);
            return DeskCards.Notice(DeskCopies.Of(c.Cell, c.Cell.ItemId is { } id ? View.FactsOf(id) : null, View.Flea, rule), rule, Name);
        }
        Assert.Equal($"970,000{Nb}₽ copied · 3% under the last low", Notice("stash", 8, 0, DeskCopyRule.Default));
        Assert.Equal($"1,000,000{Nb}₽ copied · the last low", Notice("stash", 8, 0, new DeskCopyRule(0)));
        Assert.Equal($"1,050,000{Nb}₽ copied · 5% over the last low", Notice("stash", 8, 0, new DeskCopyRule(5)));
        Assert.EndsWith("copied · 3% under the last low, scaled to 1469/1800", Notice("backpack", 4, 1, DeskCopyRule.Default));
        Assert.EndsWith("copied · 3% under the last low, over the best net", Notice("stash", 5, 2, DeskCopyRule.Default));
        Assert.Equal($"Nothing copied: Therapist pays more, 459,000{Nb}₽", Notice("pockets", 0, 0, DeskCopyRule.Default));
        Assert.Equal("Nothing copied: it can't be listed on the flea", Notice("rig", 5, 1, DeskCopyRule.Default));
        Assert.Equal("One of 3 items: pick it on the card first", Notice("stash", 4, 1, DeskCopyRule.Default));
        Assert.Equal("Nothing copied: no price for this cell", Notice("backpack", 3, 3, DeskCopyRule.Default));
    }

    // The clipboard gets the digits alone, as the flea's price field takes a paste (owner, 2026-10-19: "Copy and paste
    // worked").
    [Fact]
    public void The_clipboard_gets_the_digits()
    {
        var (_, ledx) = At("stash", 8, 0);
        Assert.Equal("970000", DeskCards.Clipboard(DeskCopies.Of(ledx.Cell, View.FactsOf(ledx.Cell.ItemId!), View.Flea, DeskCopyRule.Default)));
    }

    [Fact]
    public void Changes_and_ages_read_as_the_card_says_them()
    {
        Assert.Equal($"↑{Nb}+1.7%", DeskCards.Change(1.66));
        Assert.Equal($"↓{Nb}−10.3%", DeskCards.Change(-10.33));
        Assert.Equal("0%", DeskCards.Change(0.04));
        Assert.Equal("Prices just in · tarkov.dev", DeskCards.Age(Now, Now));
        Assert.Equal("Prices 1 min old · tarkov.dev", DeskCards.Age(Now.AddSeconds(-90), Now));
        Assert.Equal("Prices 119 min old · tarkov.dev", DeskCards.Age(Now.AddMinutes(-119), Now));
        Assert.Equal("Prices 2 h old · tarkov.dev", DeskCards.Age(Now.AddMinutes(-150), Now));
        Assert.Null(DeskCards.Age(null, Now));
    }
}
