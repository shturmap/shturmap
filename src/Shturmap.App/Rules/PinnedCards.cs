using System.Globalization;
using Shturmap.Core.Quests;

namespace Shturmap.App.Rules;

/// <summary>
/// The popped-out quest cards ("pinned" in the code and the settings) kept between runs: which quests, where their
/// windows stood, and what becomes of a card as its quest's state changes. A card is forgotten only when its quest
/// is over or the player closes its window; a quest that isn't active in the data shown now (the game switched
/// between PvE and PvP) keeps its place in the list (2026-10-04: a mode change closed every card and saved an
/// empty list).
/// </summary>
public static class PinnedCards
{
    /// <summary>The setting's key in shturmap.db: "quest@x,y;quest@x,y".</summary>
    public const string Setting = "pinned.cards";

    /// <param name="At">The window's place on the screen in pixels, or null where it wasn't saved.</param>
    public readonly record struct Entry(string QuestId, (int X, int Y)? At);

    public static IReadOnlyList<Entry> Parse(string? setting)
    {
        var entries = new List<Entry>();
        foreach (var part in (setting ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = part.Split('@', ',');
            if (fields[0].Length == 0 || entries.Any(e => e.QuestId == fields[0]))
                continue;
            (int, int)? at = fields.Length == 3
                && int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                && int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                ? (x, y)
                : null;
            entries.Add(new Entry(fields[0], at));
        }
        return entries;
    }

    public static string Format(IEnumerable<Entry> entries) =>
        string.Join(";", entries.Select(e => e.At is { } at
            ? string.Create(CultureInfo.InvariantCulture, $"{e.QuestId}@{at.X},{at.Y}")
            : e.QuestId));

    public enum Fate
    {
        /// <summary>The quest is active: its card is shown.</summary>
        Show,

        /// <summary>Nothing says the quest is active here (another mode's data, a quest this data doesn't know): no
        /// card now, but it keeps its place in the saved list.</summary>
        Wait,

        /// <summary>The log reported the quest completed or failed: the card has nothing left to show.</summary>
        Forget,
    }

    /// <summary>What becomes of a card, by its quest's state in the data shown (null: the data doesn't know the quest).</summary>
    public static Fate For(QuestState? state) => state switch
    {
        QuestState.Active => Fate.Show,
        QuestState.Completed or QuestState.Failed => Fate.Forget,
        _ => Fate.Wait,
    };
}
