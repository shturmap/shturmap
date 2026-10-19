using Shturmap.Core.Desk;

namespace Shturmap.Core.Tests.Desk;

// The stash desk's figure on a cell (docs/DESIGN.md §4, "Screen anatomy", *The stash desk*, *The cell*): where the flea's net
// at the last low after tarkov.dev's fee beats the best trader's price, the flea's listing price at the last low (owner,
// 2026-10-19), else the trader's; scaled to a printed condition, "~", "+", the "one of N" range, the tint (by what the sale
// brings), the arrow and the liquidity. Made-up items and prices.
public class DeskValuesTests
{
    private static readonly DeskFleaRules Flea = new(0.05, 0.05);

    private static DeskItemFacts Item(string id = "000000000000000000000001", double? lastLow = 20_000, double basePrice = 10_000,
        (string Trader, double Price)[]? traders = null) =>
        new(id) { LastLow = lastLow, BasePrice = basePrice, Traders = traders ?? [], Offers = 60, Change48hPercent = 0 };

    // tarkov-api's fleaMarketFee: V0·Ti·4^P0·Q + VR·Tr·4^PR·Q, the exponent of the side that is larger to the power 1.08.
    [Theory]
    [InlineData(970_000, 1_000_000, 1, 0, 0, 98_272)]
    [InlineData(10_000, 20_000, 1, 0, 0, 1_790)]
    [InlineData(10_000, 5_000, 1, 0, 0, 895)]
    [InlineData(10_000, 20_000, 3, 0, 0, 5_371)]
    [InlineData(1_000, 1_000, 1, 0, 0, 100)]
    // Intelligence Center 3 takes 30 %, each Hideout Management level 1 % of that more: at 50, 45 % off.
    [InlineData(10_000, 20_000, 1, 3, 50, 985)]
    // Below level 3 the Intelligence Center takes nothing.
    [InlineData(10_000, 20_000, 1, 2, 50, 1_790)]
    public void The_fee_is_tarkov_devs(double basePrice, double price, int count, int intel, int management, double fee) =>
        Assert.Equal(fee, DeskValues.Fee(basePrice, price, count, Flea, new DeskFeeLevels(intel, management)));

    // The owner's test of 2026-10-19: the Dorm 104 key showed "~11k" (its net) while the flea listed 15k. The cell shows the
    // listing price at the last low where the flea is the better sale; the net is the card's line, and the tint's measure.
    [Fact]
    public void The_flea_wins_when_its_net_is_more_and_shows_its_listing_price()
    {
        var value = DeskValues.Of(Item(traders: [("trader-a", 10_000)]), 1, null, null, Flea);
        Assert.Equal(DeskSource.Flea, value.Source);
        Assert.Equal(20_000, value.Value);
        Assert.Equal(20_000 - 1_790, value.FleaNet);
        Assert.Equal(20_000 - 1_790, value.Worth);
        Assert.Equal(1_790, value.Fee);
        Assert.Equal(10_000, value.TraderPrice);
        // The trader's price where the flea's net is less, though the listing price is more.
        var trader = DeskValues.Of(Item(traders: [("trader-a", 19_000)]), 1, null, null, Flea);
        Assert.Equal((DeskSource.Trader, 19_000d, 19_000d), (trader.Source, trader.Value, trader.Worth));
    }

    [Fact]
    public void The_best_trader_wins_when_it_pays_more_and_says_who()
    {
        var value = DeskValues.Of(Item(traders: [("trader-a", 15_000), ("trader-b", 19_000)]), 1, null, null, Flea);
        Assert.Equal(DeskSource.Trader, value.Source);
        Assert.Equal("trader-b", value.Trader);
        Assert.Equal(19_000, value.Value);
    }

    [Fact]
    public void A_stack_is_worth_its_count()
    {
        var value = DeskValues.Of(Item(traders: [("trader-a", 100)]), 3, null, null, Flea);
        Assert.Equal(3 * 20_000, value.Value);
        Assert.Equal(3 * 20_000 - 5_371, value.FleaNet);
    }

    [Fact]
    public void An_item_the_game_keeps_off_the_flea_has_the_traders_price()
    {
        var value = DeskValues.Of(Item(traders: [("trader-a", 5_000)]) with { NoFlea = true }, 1, null, null, Flea);
        Assert.Equal(DeskSource.Trader, value.Source);
        Assert.Equal(DeskNoFlea.NotAllowed, value.NoFlea);
        Assert.Null(value.FleaNet);
    }

    [Fact]
    public void Where_the_flea_wants_found_in_raid_an_item_without_the_mark_has_no_flea_figure()
    {
        var rules = Flea with { FoundInRaidRequired = true };
        Assert.Equal(DeskNoFlea.NotFoundInRaid, DeskValues.Of(Item(traders: [("trader-a", 5_000)]), 1, null, false, rules).NoFlea);
        Assert.Equal(DeskNoFlea.NotFoundInRaid, DeskValues.Of(Item(traders: [("trader-a", 5_000)]), 1, null, null, rules).NoFlea);
        Assert.Equal(DeskSource.Flea, DeskValues.Of(Item(traders: [("trader-a", 5_000)]), 1, null, true, rules).Source);
    }

    [Fact]
    public void No_offers_and_no_trader_is_no_figure()
    {
        var value = DeskValues.Of(Item(lastLow: null), 1, null, null, Flea);
        Assert.Equal(DeskSource.None, value.Source);
        Assert.Equal(DeskNoFlea.NoOffers, value.NoFlea);
        Assert.Null(DeskValues.Figure([(Item(lastLow: null), value)], 1, unseen: false));
    }

    [Fact]
    public void The_player_level_is_unknown_so_the_flea_level_is_carried_and_never_compared()
    {
        var item = Item(traders: [("trader-a", 100)]) with { FleaLevel = 35 };
        var value = DeskValues.Of(item, 1, null, null, Flea);
        Assert.Equal(DeskSource.Flea, value.Source);
        Assert.Equal(35, DeskValues.Figure([(item, value)], 1, false)!.FleaLevel);
    }

    // The addendum's Da: a printed condition scales the figure by its share and keeps the "~" ("a Grizzly at 1469/1800,
    // ~31k, not its full 37k"); meds, food and drink at a full print drop it; armour keeps it.
    [Fact]
    public void A_printed_condition_scales_the_figure_and_keeps_the_estimate()
    {
        var grizzly = Item(lastLow: 40_000, basePrice: 20_000) with { Kind = DeskItemKind.Meds };
        var value = DeskValues.Of(grizzly, 1, new DeskPrint(1469, 1800), null, Flea);
        Assert.True(value.Estimate);
        Assert.Equal(Math.Round(value.Full * 1469 / 1800), value.Value);
        Assert.True(value.Value < value.Full);
    }

    // A full print drops the "~" on anything but armour (owner, 2026-10-19: a key at 40/40 showed "~11k"); one below full
    // keeps it, scaled.
    [Fact]
    public void Meds_food_drink_and_keys_at_a_full_print_are_no_estimate()
    {
        var key = DeskValues.Of(Item(), 1, new DeskPrint(39, 40), null, Flea);
        Assert.True(key.Estimate);
        Assert.Equal(Math.Round(20_000 * 39 / 40.0), key.Value);
        foreach (var kind in new[] { DeskItemKind.Meds, DeskItemKind.FoodDrink, DeskItemKind.None })
        {
            var value = DeskValues.Of(Item() with { Kind = kind }, 1, new DeskPrint(3, 3), null, Flea);
            Assert.False(value.Estimate);
            Assert.Equal(value.Full, value.Value);
        }
    }

    [Fact]
    public void Armour_is_an_estimate_at_a_full_print_and_without_one()
    {
        Assert.True(DeskValues.Of(Item() with { Kind = DeskItemKind.Armour }, 1, new DeskPrint(147, 147), null, Flea).Estimate);
        Assert.True(DeskValues.Of(Item() with { Kind = DeskItemKind.Armour }, 1, null, null, Flea).Estimate);
        Assert.True(DeskValues.Of(Item() with { Kind = DeskItemKind.Meds }, 1, null, null, Flea).Estimate);
        Assert.False(DeskValues.Of(Item(), 1, null, null, Flea).Estimate);
    }

    [Fact]
    public void A_magazines_or_a_weapons_print_is_its_rounds_not_a_condition()
    {
        var magazine = DeskValues.Of(Item() with { Kind = DeskItemKind.Magazine }, 1, new DeskPrint(30, 60), null, Flea);
        Assert.Equal(magazine.Full, magazine.Value);
        Assert.False(magazine.Estimate);
        var gun = DeskValues.Of(Item() with { Kind = DeskItemKind.Gun }, 1, new DeskPrint(30, 60), null, Flea);
        Assert.Equal(gun.Full, gun.Value);
        Assert.True(gun.Estimate);
    }

    [Fact]
    public void One_of_n_is_the_candidates_range_counted_at_its_least()
    {
        var a = Item("000000000000000000000001", lastLow: null, traders: [("trader-a", 63_000)]);
        var b = Item("000000000000000000000002", lastLow: null, traders: [("trader-a", 307_000)]);
        var figure = DeskValues.Figure([(a, DeskValues.Of(a, 1, null, null, Flea)), (b, DeskValues.Of(b, 1, null, null, Flea))], 1, false)!;
        Assert.True(figure.Unsure);
        Assert.Equal(63_000, figure.Value);
        Assert.Equal(307_000, figure.High);
        Assert.Equal(DeskSource.None, figure.Source);
        Assert.Equal(DeskArrow.None, figure.Arrow);
    }

    [Theory]
    [InlineData(19_999, 1, 0)]
    [InlineData(20_000, 1, 1)]
    [InlineData(100_000, 1, 2)]
    [InlineData(300_000, 1, 3)]
    // Per slot: 300k over two cells is 150k a slot.
    [InlineData(300_000, 2, 2)]
    public void The_tint_steps_by_value_per_slot(double price, int cells, int tint)
    {
        var item = Item(lastLow: null, traders: [("trader-a", price)]);
        Assert.Equal(tint, DeskValues.Figure([(item, DeskValues.Of(item, 1, null, null, Flea))], cells, false)!.Tint);
    }

    [Theory]
    [InlineData(10.0, DeskArrow.Up)]
    [InlineData(9.99, DeskArrow.None)]
    [InlineData(-10.0, DeskArrow.Down)]
    [InlineData(-9.99, DeskArrow.None)]
    public void The_arrow_shows_from_ten_percent_in_48_hours(double change, DeskArrow arrow) =>
        Assert.Equal(arrow, DeskValues.Arrow(change));

    [Theory]
    [InlineData(50, DeskLiquidity.Full)]
    [InlineData(49, DeskLiquidity.Half)]
    [InlineData(10, DeskLiquidity.Half)]
    [InlineData(9, DeskLiquidity.Ring)]
    [InlineData(null, DeskLiquidity.Unknown)]
    public void The_liquidity_dot_comes_from_the_offers(int? offers, DeskLiquidity liquidity) =>
        Assert.Equal(liquidity, DeskValues.Liquidity(offers));

    [Fact]
    public void Contents_unseen_put_a_plus_on_the_figure()
    {
        var item = Item(traders: [("trader-a", 100)]);
        Assert.True(DeskValues.Figure([(item, DeskValues.Of(item, 1, null, null, Flea))], 1, unseen: true)!.Plus);
    }

    [Theory]
    [InlineData("46/50", 46, 50)]
    [InlineData("1469/1800", 1469, 1800)]
    [InlineData("4ØØ/4ØØ", 400, 400)]
    [InlineData("S0/50", 50, 50)]
    [InlineData("1/4", 1, 4)]
    public void A_print_is_read_with_the_games_slashed_zero(string text, int current, int max) =>
        Assert.Equal(new DeskPrint(current, max), DeskPrint.Parse(text));

    [Theory]
    [InlineData("60")]
    [InlineData("51/50")]
    [InlineData("a/b")]
    [InlineData("/50")]
    [InlineData(null)]
    public void What_is_no_print_reads_as_none(string? text) => Assert.Null(DeskPrint.Parse(text));
}
