using Shturmap.Core;

namespace Shturmap.App.Rules;

/// <summary>
/// What the QUEST COMPLETE cue says (owner, 2026-10-07: "For completed quests, also make a nice animation"; docs/DESIGN.md
/// §4, principle 11). Quests are handed in a few at a time (a week of the study log: twenty-odd completions, all in
/// the menus, most of them two to four within a minute and a half), so the ones that come while the cue is up join it.
/// </summary>
public static class CompletionWords
{
    /// <summary>A completed quest: its name, its trader, the quests that need it done.</summary>
    public sealed record Done(string Name, string Trader, IReadOnlyList<string> Unlocks);

    /// <summary>The cue's eyebrow, title and detail: "QUEST COMPLETE · PRAPOR", the quest, what it unlocks; for several,
    /// "QUESTS COMPLETE", how many, their names and what they unlock together.</summary>
    public static (string Eyebrow, string Title, string Detail) Of(IReadOnlyList<Done> done)
    {
        if (done.Count == 0)
            return ("", "", "");
        if (done.Count == 1)
        {
            var one = done[0];
            return (one.Trader.Length > 0 ? RuleTexts.CompleteOneFrom(trader: one.Trader.ToUpper(UiLanguage.Culture)) : RuleTexts.CompleteOne,
                one.Name, Unlocks(one.Unlocks));
        }
        var names = Listed(done.Select(d => d.Name).ToList(), 3);
        var unlocks = Unlocks(done.SelectMany(d => d.Unlocks).Distinct().ToList());
        return (RuleTexts.CompleteSeveral, RuleTexts.CompleteSeveralCount(count: done.Count), unlocks.Length > 0 ? $"{names}\n{unlocks}" : names);
    }

    // "Unlocks Setup and Shooter Born in Heaven", or nothing when it unlocks nothing.
    private static string Unlocks(IReadOnlyList<string> names) => names.Count == 0 ? "" : RuleTexts.CompleteUnlocks(quests: Listed(names, 3));

    // "A", "A and B", "A, B and C", "A, B, C and 2 more".
    private static string Listed(IReadOnlyList<string> names, int most) => names.Count switch
    {
        1 => names[0],
        _ when names.Count <= most => RuleTexts.ListLast(list: string.Join(RuleTexts.ListSeparator, names.Take(names.Count - 1)), last: names[^1]),
        _ => RuleTexts.ListMore(count: names.Count - most, list: string.Join(RuleTexts.ListSeparator, names.Take(most))),
    };
}
