namespace Shturmap.App.Rules;

/// <summary>One trader in the strip under the MAP list: the trader, how many active quests they gave, and which.</summary>
/// <param name="Quests">Their active quests' ids: pointing at the portrait lights these, as a boss's name lights its zones.</param>
public sealed record TraderEntry(string TraderId, int Count, IReadOnlyList<string> Quests)
{
    /// <summary>Whether two entries say and light the same: then the strip on screen stays (Rules.RowLists).</summary>
    public static bool Same(TraderEntry a, TraderEntry b) =>
        a.TraderId == b.TraderId && a.Count == b.Count && a.Quests.SequenceEqual(b.Quests);
}

/// <summary>
/// The trader focus (owner, 2026-10-17: "some players want to level a specific trader or want to work towards specific
/// unlocks of these traders. Currently there are no good ways in the app to account for that, you can only select by
/// map and not by trader. I at the same time want to keep the UI footprint lean"; from a panel of three, "B", a trader
/// strip). A row under the MAP list holds a portrait for each trader with active quests, with their count. Pointing at
/// one lights that trader's quests, as pointing at a boss's name lights its zones; a click holds the focus: the rows
/// and the map step the other quests back, and the maps' counts are the trader's. One trader at a time; a click on the
/// held portrait or Esc lets go. It lives for the app's run: not saved, and kept through raids, a new quest list and a
/// change of map until it is let go.
/// </summary>
public static class TraderFocus
{
    /// <summary>How strongly a row of another trader's quest is drawn while a focus is held: the map's labels' measure in
    /// planning in a held focus's static look ("Map drawing", "Stepping back"; owner, 2026-10-17, "B": 45 % until then,
    /// the measure of one quest pointed at). Above the 28 % the owner found too dark.</summary>
    public const double SteppedBack = 0.3;

    /// <summary>
    /// The strip: each trader with an active quest, in the game's trader order (tarkov.dev's list, as Plan's order
    /// takes it), with how many and which. A trader the data doesn't list comes after, by id. The held trader keeps
    /// its place with none left, so the focus can still be seen and let go.
    /// </summary>
    /// <param name="active">Each active quest and its trader (null: the data names none).</param>
    /// <param name="order">The traders' ids in the game's order.</param>
    /// <param name="held">The trader whose focus is held, or null.</param>
    public static IReadOnlyList<TraderEntry> Strip(IEnumerable<(string QuestId, string? TraderId)> active, IReadOnlyList<string> order, string? held)
    {
        var quests = active.Where(q => q.TraderId is not null)
            .GroupBy(q => q.TraderId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(q => q.QuestId).Distinct(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        if (held is not null && !quests.ContainsKey(held))
            quests[held] = [];
        int Place(string trader) => order.ToList().IndexOf(trader) is var at and >= 0 ? at : int.MaxValue;
        return quests.OrderBy(p => Place(p.Key)).ThenBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new TraderEntry(p.Key, p.Value.Count, p.Value))
            .ToList();
    }

    /// <summary>
    /// What the strip shows: every trader between raids; in a raid (or while one loads) only the held one, so the
    /// player sees the focus and can let go of it after the raid, or nothing (no clicks in a raid, §4).
    /// </summary>
    public static IReadOnlyList<TraderEntry> Shown(IReadOnlyList<TraderEntry> strip, string? held, bool inRaid) =>
        !inRaid ? strip : held is null ? [] : strip.Where(t => t.TraderId == held).ToList();

    /// <summary>A click on a portrait: it holds that trader's focus, in place of any other; on the held one it lets go.</summary>
    public static string? Click(string? held, string trader) => held == trader ? null : trader;

    /// <summary>
    /// Esc: it closes the cards, as before; with no card open it lets go of the focus. One key for both, the cards
    /// first, so closing a card never throws away the focus as well.
    /// </summary>
    /// <returns>The focus after the key, and whether the cards close.</returns>
    public static (string? Held, bool CloseCards) Escape(string? held, bool cardsOpen) => cardsOpen ? (held, true) : (null, false);

    /// <summary>
    /// Whether a quest's row steps back: between raids, while a focus is held, when the quest is another trader's. A
    /// pick never does, as on the map (they are the plan for this raid), and in a raid the raid card is unchanged.
    /// </summary>
    public static bool StepsBack(string? held, bool inRaid, string? questTrader, bool picked = false) =>
        held is not null && !inRaid && !picked && questTrader != held;

    /// <summary>
    /// Whether a BRING row steps back: between raids, while a focus is held, when none of the quests it serves is the
    /// trader's or picked.
    /// </summary>
    /// <param name="serves">The traders of the quests it is for, and whether each is picked.</param>
    public static bool StepsBackServing(string? held, bool inRaid, IEnumerable<(string? Trader, bool Picked)> serves) =>
        held is not null && !inRaid && !serves.Any(q => q.Picked || q.Trader == held);

    /// <summary>
    /// The quests the map draws as the focus while nothing is pointed at: the held trader's active quests, in a raid
    /// too, without the pulse (only what the pointer is on pulses). Empty without a focus.
    /// </summary>
    public static IReadOnlySet<string> OnMap(string? held, IEnumerable<(string QuestId, string? TraderId)> active) =>
        held is null ? new HashSet<string>() : active.Where(q => q.TraderId == held).Select(q => q.QuestId).ToHashSet(StringComparer.Ordinal);

    /// <summary>A portrait's tooltip, with what a click does: "Ragman · 4 active quests · click to hold", or while its
    /// focus is held "… · click to let go".</summary>
    public static string Tip(string traderName, int count, bool held) =>
        held ? RuleTexts.TraderStripTipHeld(trader: traderName, count: count) : RuleTexts.TraderStripTip(trader: traderName, count: count);
}
