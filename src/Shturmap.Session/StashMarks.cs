using Shturmap.Core;
using Shturmap.Core.Planning;
using Shturmap.Core.Stash;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session;

/// <summary>What a BRING row says of the item's mark: the ink line, and how many are still to get.</summary>
/// <param name="Text">"Seen in your stash · 5 Oct", "3 of 7: given with Provide Viewership · 1 Oct".</param>
/// <param name="More">How many the row still needs beyond what the mark says; 0 when the mark covers the row.</param>
public sealed record StashRowView(string Text, int More);

/// <summary>
/// The words of a mark (docs/DESIGN.md §4, "What you have"): an observation with its source and date, never "you have
/// it". Counts are "at least"; a row that needs more than was seen says its part and leaves the amber line to say
/// where to get the rest.
/// </summary>
public static class StashMarks
{
    /// <summary>The row's mark, or null when nothing is known of any item the row stands for.</summary>
    public static StashRowView? ForRow(GameData data, RequirementView row, IReadOnlyDictionary<string, StashMark> marks)
    {
        if (row.Kind == RequirementKind.Weapon || row.Kind == RequirementKind.Exit || row.Kind == RequirementKind.Entry)
            return null; // weapons aren't read; an exit's or entry's item is the row's purpose, not something to have
        var ids = row.Alternatives is { Count: > 0 } all ? all : [row.ItemId];
        // Of several items that will do, the one most of was seen.
        var mark = ids.Select(id => marks.GetValueOrDefault(id)).OfType<StashMark>().OrderByDescending(m => m.Count).ThenByDescending(m => m.SeenAt).FirstOrDefault();
        if (mark is null || mark.Source == MarkSource.You)
            return null;
        var needed = Math.Max(1, row.Count);
        var date = UiLanguage.DayMonth(mark.SeenAt.LocalDateTime);
        var seen = mark.Count;
        string text;
        switch (mark.Source)
        {
            case MarkSource.Screenshot:
                if (NeedsFoundInRaid(data, row, mark.ItemId))
                {
                    // Only what carried the game's mark counts for a hand-over that asks for it.
                    seen = mark.FoundInRaid;
                    text = seen == 0 ? SessionTexts.MarkSeenNotFir(date: date)
                        : seen >= needed ? SessionTexts.MarkSeenFir(date: date)
                        : SessionTexts.MarkSeenPartFir(seen: Number(seen), needed: Number(needed), date: date);
                }
                else
                {
                    text = seen >= needed ? SessionTexts.MarkSeen(date: date) : SessionTexts.MarkSeenPart(seen: Number(seen), needed: Number(needed), date: date);
                }
                if (mark.NotSeenAt is { } since)
                    text = SessionTexts.MarkNotSince(mark: text, date: UiLanguage.DayMonth(since.LocalDateTime));
                break;
            default:
                var quest = mark.QuestId is { } questId ? data.Tasks.GetValueOrDefault(questId)?.Name ?? questId : "";
                var part = seen < needed;
                // "The quest" when the row is that quest's alone; named when the row serves several.
                var own = row.QuestIds.Count == 1 && row.QuestIds[0] == mark.QuestId;
                text = mark.Grant switch
                {
                    GrantKind.QuestReward => part ? SessionTexts.MarkRewardPart(seen: Number(seen), needed: Number(needed), quest: quest, date: date) : SessionTexts.MarkReward(quest: quest, date: date),
                    GrantKind.Insurance => part ? SessionTexts.MarkInsurancePart(seen: Number(seen), needed: Number(needed), date: date) : SessionTexts.MarkInsurance(date: date),
                    _ => part
                        ? own ? SessionTexts.MarkGivenPartOwn(seen: Number(seen), needed: Number(needed), date: date) : SessionTexts.MarkGivenPart(seen: Number(seen), needed: Number(needed), quest: quest, date: date)
                        : own ? SessionTexts.MarkGiven(date: date) : SessionTexts.MarkGivenBy(quest: quest, date: date),
                };
                break;
        }
        return new StashRowView(text, Math.Max(0, needed - seen));
    }

    /// <summary>The item card's STASH line for a mark: with the count when more than one was seen, and the time.</summary>
    public static string ForCard(GameData data, StashMark mark)
    {
        var date = UiLanguage.DayMonthTime(mark.SeenAt.LocalDateTime);
        var count = Number(mark.Count);
        var quest = mark.QuestId is { } questId ? data.Tasks.GetValueOrDefault(questId)?.Name ?? questId : "";
        var text = mark.Source switch
        {
            MarkSource.Screenshot when mark.Count > 1 => mark.FoundInRaid >= mark.Count ? SessionTexts.MarkSeenCountFir(count: count, date: date) : SessionTexts.MarkSeenCount(count: count, date: date),
            MarkSource.Screenshot => mark.FoundInRaid >= 1 ? SessionTexts.MarkSeenFir(date: date) : SessionTexts.MarkSeen(date: date),
            _ => mark.Grant switch
            {
                GrantKind.QuestReward => mark.Count > 1 ? SessionTexts.MarkRewardCount(count: count, quest: quest, date: date) : SessionTexts.MarkReward(quest: quest, date: date),
                GrantKind.Insurance => mark.Count > 1 ? SessionTexts.MarkInsuranceCount(count: count, date: date) : SessionTexts.MarkInsurance(date: date),
                _ => mark.Count > 1 ? SessionTexts.MarkGivenCount(count: count, quest: quest, date: date) : SessionTexts.MarkGivenBy(quest: quest, date: date),
            },
        };
        return mark.Source == MarkSource.Screenshot && mark.NotSeenAt is { } since ? SessionTexts.MarkNotSince(mark: text, date: UiLanguage.DayMonth(since.LocalDateTime)) : text;
    }

    /// <summary>Whether a quest of the row asks for the item found in raid (a hand-over or a find with the game's mark).</summary>
    public static bool NeedsFoundInRaid(GameData data, RequirementView row, string itemId) =>
        row.QuestIds.Select(id => data.Tasks.GetValueOrDefault(id)).OfType<ApiTask>()
            .SelectMany(t => t.Objectives ?? [])
            .Any(o => o.FoundInRaid && (o.Items ?? []).Contains(itemId));

    private static string Number(int n) => n.ToString("N0", UiLanguage.Culture);
}
