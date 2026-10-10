#if DEVTOOLS
using System.Globalization;
using Shturmap.App.Rules;
using Shturmap.Core.Text;

namespace Shturmap.Core.Tests;

// The layout check's words (developer builds; docs/LANGUAGES.md, "Layout check"): in the pseudo-language, what is shown
// outside its brackets is a text that isn't in the texts files yet, unless it stays the same in every language.
public class LayoutWordsTests
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    private static readonly HashSet<string> Names =
        new(new[] { "Customs", "Streets of Tarkov", "Klimov Street (Flare)", "Kaban", "MS2000 Marker", "Praetorian", "Map" }.Select(LayoutWords.Key));

    private static IReadOnlyList<string> English(string text) => LayoutWords.EnglishParts(text, Names, Us);

    [Fact]
    public void A_text_of_the_pseudo_language_is_none_also_with_a_game_name_in_it()
    {
        Assert.Empty(English(PseudoText.Of("Bring: {item}").Replace("{item}", "MS2000 Marker", StringComparison.Ordinal)));
        Assert.Empty(English(PseudoText.Of("Not in a raid")));
    }

    [Fact]
    public void A_text_inside_another_ones_placeholder_is_one_too()
    {
        var inner = PseudoText.Of("Night");
        Assert.Empty(English(PseudoText.Of("In raid: {map}").Replace("{map}", inner, StringComparison.Ordinal)));
    }

    [Fact]
    public void An_english_text_is_reported_with_its_words()
    {
        Assert.Equal(["Delete position screenshots"], English("Delete position screenshots"));
        Assert.Equal(["NOT IN RAID"], English("NOT IN A RAID"));
    }

    [Fact]
    public void English_glued_to_a_text_of_the_pseudo_language_is_reported_alone()
    {
        Assert.Equal(["quests"], English(PseudoText.Of("Complete") + " 5 quests"));
        Assert.Equal(["FIND AUTOMATICALLY"], English(PseudoText.Of("A chosen folder") + " · FIND AUTOMATICALLY"));
    }

    [Fact]
    public void Game_names_figures_keys_dates_paths_and_the_short_list_stay_as_they_are()
    {
        Assert.Empty(English("CUSTOMS · PMC · 12 MIN"));
        Assert.Empty(English("Kaban 75%"));
        Assert.Empty(English("Klimov Street (Flare) · 214 m"));
        Assert.Empty(English("Streets of Tarkov"));
        Assert.Empty(English("Shift+F"));
        Assert.Empty(English("F1 / ?"));
        Assert.Empty(English("PrtSc"));
        Assert.Empty(English("25 Sep"));
        Assert.Empty(English("Shturmap 0.4.0+d0e71e1 \"Praetorian\""));
        Assert.Empty(English(@"%LOCALAPPDATA%\Shturmap\logs"));
        Assert.Empty(English("tarkov.dev"));
        Assert.Empty(English("×3"));
    }

    [Fact]
    public void A_game_name_among_english_words_leaves_the_english_reported()
    {
        Assert.Equal(["Raid on Customs"], English("Raid on Customs"));
        Assert.Equal(["MIN"], English("MIN"));
        // An item called "Map" doesn't hide the word: a name of one word counts only among names and figures.
        Assert.Equal(["WIKI MAP"], English("WIKI MAP"));
        Assert.Empty(English("Kaban 75% · Streets of Tarkov"));
    }

    private sealed record Thing(string Id, string Name, string? ShortName, string Type, string? Description = null, string? Conditions = null);

    private sealed record World(Dictionary<string, Thing> Things, IReadOnlyDictionary<string, string> NamesById, List<string> Ids, Thing? Missing,
        IReadOnlyDictionary<string, string> ItemShortNames);

    [Fact]
    public void Names_are_taken_from_names_descriptions_conditions_and_dictionaries_of_texts_not_from_short_names_internal_words_or_ids()
    {
        var world = new World(
            new Dictionary<string, Thing>
            {
                ["a"] = new("5c0e534186f7747fa1419867", "MS2000 Marker", "MS2000", "visit", "Mark the first LAV III"),
                // A transit's conditions, as tarkov.dev words them.
                ["t"] = new("5c0e534186f7747fa1419869", "Transit to The Lab", null, "transit", Conditions: "TerraGroup Labs access keycard required (1)"),
            },
            new Dictionary<string, string> { ["b"] = "Bomber beanie", ["c"] = "5c0e534186f7747fa1419868" },
            ["shoot"],
            null,
            new Dictionary<string, string> { ["d"] = "Log" });
        var names = LayoutWords.NamesIn(world);
        Assert.Contains("MS2000 MARKER", names);
        Assert.DoesNotContain("MS2000", names);
        Assert.DoesNotContain("LOG", names);
        Assert.Contains("MARK THE FIRST LAV III", names);
        Assert.Contains("TERRAGROUP LABS ACCESS KEYCARD REQUIRED (1)", names);
        Assert.Contains("BOMBER BEANIE", names);
        Assert.DoesNotContain("VISIT", names);
        Assert.DoesNotContain("SHOOT", names);
        Assert.DoesNotContain("5C0E534186F7747FA1419868", names);
    }
}
#endif
