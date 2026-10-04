using SkiaSharp;

namespace Shturmap.Map.Tests;

// The help panel's legend is drawn by the map's renderer, one row per symbol (cartography review, 2026-10-02).
public class LegendTests
{
    [Fact]
    public void Every_symbol_has_one_row()
    {
        Assert.Equal(Enum.GetValues<LegendSymbol>(), MapLegend.Rows.Select(r => r.Symbol).Order());
        Assert.All(MapLegend.Rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Text)));
    }

    [Theory]
    [MemberData(nameof(Symbols))]
    public void Every_swatch_draws_inside_its_box(LegendSymbol symbol)
    {
        using var bitmap = MapLegend.Draw(symbol, 2);
        Assert.Equal((112, 68), (bitmap.Width, bitmap.Height));
        var drawn = bitmap.Pixels.Count(p => p.Alpha > 0);
        Assert.True(drawn > 40, $"{symbol}: {drawn} pixels drawn");
        // Nothing runs into the edges (a clipped badge or label).
        var edge = Enumerable.Range(0, bitmap.Width).SelectMany(x => new[] { bitmap.GetPixel(x, 0), bitmap.GetPixel(x, bitmap.Height - 1) })
            .Concat(Enumerable.Range(0, bitmap.Height).SelectMany(y => new[] { bitmap.GetPixel(0, y), bitmap.GetPixel(bitmap.Width - 1, y) }));
        Assert.True(symbol is LegendSymbol.Guide || edge.All(p => p.Alpha < 40), $"{symbol} touches the edge of its swatch");
    }

    public static TheoryData<LegendSymbol> Symbols() => new(Enum.GetValues<LegendSymbol>());

    // Help lists the symbols of the map on screen first (owner, 2026-10-04): a symbol counts when the scene holds
    // what it stands for.
    [Fact]
    public void A_map_lists_the_symbols_it_holds_and_no_others()
    {
        var at = new Shturmap.Core.WorldPoint(0, 0, 0);
        var (_, scene) = TestView.Of(
        [
            TestView.Quest("a", 0, 0, "Quest"),
            new MapMarker("extract:1", MarkerKind.ExtractPmc, at, "Gate"),
            new MapMarker("boss:1", MarkerKind.BossSpawn, at, "Boss 50%"),
        ]);
        var on = MapLegend.On(scene);
        // A quest with a place here can be pointed at, and its places out of view then get gold chevrons.
        foreach (var there in new[] { LegendSymbol.Objective, LegendSymbol.Extract, LegendSymbol.Boss, LegendSymbol.PointedOutOfView })
            Assert.Contains(there, on);
        // No position, no pick, no lock, no transit, one place only: none of their symbols.
        foreach (var absent in new[] { LegendSymbol.Player, LegendSymbol.Ping, LegendSymbol.KeptQuest, LegendSymbol.KeptLock, LegendSymbol.Guide,
                     LegendSymbol.OutOfView, LegendSymbol.Lock, LegendSymbol.Transit, LegendSymbol.Cluster, LegendSymbol.Optional, LegendSymbol.Done,
                     LegendSymbol.Sniper, LegendSymbol.Hazard, LegendSymbol.Trail })
            Assert.DoesNotContain(absent, on);
    }

    // The review of 2026-10-04 (B5): the legend had no row for the gold chevrons of a pointed-at quest, a picked
    // quest's cyan padlock and the ping. Each belongs where its cause can occur.
    [Fact]
    public void A_picks_door_is_listed_only_where_a_pick_needs_a_key_here()
    {
        var at = new Shturmap.Core.WorldPoint(0, 0, 0);
        var (_, scene) = TestView.Of(
        [
            TestView.Quest("a", 0, 0, "Quest"),
            new MapMarker("lock:1", MarkerKind.Lock, at, "Key", "key:one"),
        ]);
        scene.QuestKeys = new Dictionary<string, IReadOnlyList<string>> { ["quest-a"] = ["key:one"] };
        Assert.Contains(LegendSymbol.Lock, MapLegend.On(scene));
        Assert.DoesNotContain(LegendSymbol.KeptLock, MapLegend.On(scene));

        scene.Kept = new HashSet<string> { "quest-a" };
        Assert.Contains(LegendSymbol.KeptLock, MapLegend.On(scene));
        // The pick's own chevrons are cyan; the gold ones stay possible for whatever is pointed at.
        Assert.Contains(LegendSymbol.OutOfView, MapLegend.On(scene));
        Assert.Contains(LegendSymbol.PointedOutOfView, MapLegend.On(scene));
    }

    [Fact]
    public void The_new_swatches_wear_the_colours_their_rows_name()
    {
        static bool Has(SKBitmap bitmap, string hex)
        {
            var c = SKColor.Parse(hex);
            return bitmap.Pixels.Any(p => p.Alpha == 255 && Math.Abs(p.Red - c.Red) <= 3 && Math.Abs(p.Green - c.Green) <= 3 && Math.Abs(p.Blue - c.Blue) <= 3);
        }
        using var pointed = MapLegend.Draw(LegendSymbol.PointedOutOfView, 2);
        Assert.True(Has(pointed, Palette.Amber) && !Has(pointed, Palette.Pick1), "a gold chevron");
        using var picked = MapLegend.Draw(LegendSymbol.OutOfView, 2);
        Assert.True(Has(picked, Palette.Pick1) && !Has(picked, Palette.Amber), "a chevron in the pick's colour");
        using var door = MapLegend.Draw(LegendSymbol.KeptLock, 2);
        Assert.True(Has(door, Palette.Pick1), "a padlock in the pick's colour");
        using var ping = MapLegend.Draw(LegendSymbol.Ping, 2);
        Assert.True(Has(ping, Palette.Sand), "sand rings");
    }

    // Sand is the player's alone (the review of 2026-10-04, B3): a marker's floor arrow wears the marker's colour,
    // like its count and OPT badges, and a loose item's square is ink.
    [Fact]
    public void A_floor_arrow_and_a_loose_items_square_are_not_the_players_sand()
    {
        static bool Sandy(SKColor p) => p.Alpha > 200 && p.Red > 225 && p.Blue > 185;
        using var floors = MapLegend.Draw(LegendSymbol.OtherFloor, 2);
        Assert.DoesNotContain(floors.Pixels, Sandy);
        using var loose = MapLegend.Draw(LegendSymbol.LooseItem, 2);
        Assert.DoesNotContain(loose.Pixels, Sandy);
        var ink = SKColor.Parse(Palette.Ink);
        Assert.Contains(loose.Pixels, p => p.Alpha == 255 && Math.Abs(p.Red - ink.Red) <= 3 && Math.Abs(p.Green - ink.Green) <= 3 && Math.Abs(p.Blue - ink.Blue) <= 3);
        // The player's own swatch still is.
        using var player = MapLegend.Draw(LegendSymbol.Player, 2);
        Assert.Contains(player.Pixels, Sandy);
    }

    [Fact]
    public void A_position_a_pick_and_a_second_place_bring_their_symbols()
    {
        var at = new Shturmap.Core.WorldPoint(0, 0, 0);
        var (_, scene) = TestView.Of(
        [
            new MapMarker("objective:a:1", MarkerKind.Objective, at, "Quest", "quest-a", Shturmap.Core.Quests.ObjectiveKind.Exploration, Optional: true),
            new MapMarker("objective:a:2", MarkerKind.PossibleLocation, at, "Quest", "quest-a", Shturmap.Core.Quests.ObjectiveKind.Exploration),
        ]);
        scene.Player = new PlayerFix(at, 0, DateTime.UnixEpoch);
        scene.Kept = new HashSet<string> { "quest-a" };
        var on = MapLegend.On(scene);
        foreach (var there in new[] { LegendSymbol.Player, LegendSymbol.PlayerOutOfView, LegendSymbol.Ping, LegendSymbol.KeptQuest, LegendSymbol.Guide,
                     LegendSymbol.OutOfView, LegendSymbol.PointedOutOfView, LegendSymbol.PossibleLocation, LegendSymbol.Cluster, LegendSymbol.Optional })
            Assert.Contains(there, on);
    }
}
