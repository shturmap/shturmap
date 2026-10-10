using System.Text.RegularExpressions;
using Shturmap.App.Rules;
using Shturmap.Core.Text;

namespace Shturmap.Core.Tests;

// The tour (owner, 2026-10-09; docs/DESIGN.md §4, "Screen anatomy", *The tour*). Its chapters come from docs/tour.md,
// built into the app; these checks keep that file in step with the window it points at, so a renamed or removed part
// fails here rather than leaving a chapter that frames nothing.
public class TourTests
{
    private const string Sample = """
        # The tour

        Explanations above the first chapter are no chapter.
        - reads · Not a word · before any chapter

        ## safe
        WHAT IT READS
        Reads the game's logs.
        Never touches the game.
        - reads · The game's logs
        - never · Touches the game

        ## next · PlanRows | MapPicker, Map
        NEXT RAID
        Maps ranked by what your quests can do there.

        ## know · HelpButton
        GOOD TO KNOW
        - HelpButton · HELP · F1
        """;

    [Fact]
    public void Each_chapter_has_its_stage_anchors_title_lines_and_words()
    {
        var chapters = Tour.Parse(Sample);
        Assert.Equal(["safe", "next", "know"], chapters.Select(c => c.Stage));
        Assert.Equal("WHAT IT READS", chapters[0].Title);
        Assert.Equal(["Reads the game's logs.", "Never touches the game."], chapters[0].Lines);
        Assert.Equal([new Tour.Word("reads", "The game's logs"), new Tour.Word("never", "Touches the game")], chapters[0].Words);
        // A part, and the one framed when it isn't shown.
        Assert.Equal(["PlanRows", "MapPicker"], chapters[1].Anchors[0]);
        Assert.Equal(["Map"], chapters[1].Anchors[1]);
        // A word's text may hold the separator itself; the key is what stands before the first.
        Assert.Equal(new Tour.Word("HelpButton", "HELP · F1"), chapters[2].Words[0]);
        Assert.Empty(chapters[2].Lines);
    }

    [Fact]
    public void Steps_go_forward_and_back_and_end_after_the_last()
    {
        Assert.Equal(1, Tour.Step(0, forward: true, count: 7));
        Assert.Null(Tour.Step(6, forward: true, count: 7));
        Assert.Equal(2, Tour.Step(3, forward: false, count: 7));
        Assert.Equal(0, Tour.Step(0, forward: false, count: 7));
        Assert.Equal("TOUR · 3 OF 7", Tour.Eyebrow(2, 7));
    }

    [Fact]
    public void It_starts_by_itself_only_at_a_first_start()
    {
        Assert.True(Tour.StartsByItself(tourSeen: null, helpSeen: null));
        // Someone who used Shturmap before the tour existed: What's New tells them about it instead.
        Assert.False(Tour.StartsByItself(tourSeen: null, helpSeen: "1"));
        Assert.False(Tour.StartsByItself(tourSeen: "0.4.0", helpSeen: "1"));
    }

    [Theory]
    [InlineData("tour", 0)]
    [InlineData("tour:5", 4)]
    [InlineData("tour:0", null)]
    [InlineData("tour:x", null)]
    [InlineData("replay", null)]
    public void A_whats_new_line_names_its_chapter(string preview, int? chapter)
    {
        Assert.Equal(chapter, Tour.ChapterOf(preview));
        Assert.Equal(chapter is not null || WhatsNew.Previews.Contains(preview), WhatsNew.Known(preview));
    }

    // The app's own tour: short, every stage one the app draws, and every part it frames there in the window.
    [Fact]
    public void The_apps_tour_keeps_to_its_rules_and_points_at_real_parts()
    {
        var root = RepositoryRoot();
        var chapters = Tour.Parse(File.ReadAllText(Path.Combine(root, "docs", "tour.md")));
        var names = Regex.Matches(File.ReadAllText(Path.Combine(root, "src", "Shturmap.App", "MainWindow.xaml")), "x:Name=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        Assert.InRange(chapters.Count, 1, Tour.MaxChapters);
        Assert.Equal(chapters.Count, chapters.Select(c => c.Stage).Distinct().Count());
        Assert.All(chapters, chapter =>
        {
            Assert.Contains(chapter.Stage, Tour.Stages);
            Assert.True(chapter.Title.Length <= 24 && chapter.Title == chapter.Title.ToUpperInvariant(), $"{chapter.Stage}: the title '{chapter.Title}' is no band title");
            Assert.InRange(chapter.Lines.Count, 1, Tour.MaxLines);
            Assert.All(chapter.Lines, line => Assert.True(line.Length <= 100, $"{chapter.Stage}: '{line}' is longer than a line of the band"));
            Assert.All(chapter.Anchors.SelectMany(a => a), name => Assert.True(names.Contains(name), $"{chapter.Stage}: no part named '{name}' in MainWindow.xaml"));
        });
        // The stages' own words: what is read and never done; the buttons named, by their parts' names.
        var safe = chapters.Single(c => c.Stage == "safe");
        Assert.All(safe.Words, w => Assert.Contains(w.Key, new[] { "reads", "never" }));
        Assert.Contains(safe.Words, w => w.Key == "never");
        Assert.All(chapters.Single(c => c.Stage == "know").Words, w => Assert.True(names.Contains(w.Key), $"know: no part named '{w.Key}'"));
        // The extract list's plate: what to press, with the key filled in, and what was read.
        var key = chapters.Single(c => c.Stage == "key").Words;
        Assert.Contains(key, w => w.Key == "list" && w.Text.Contains("{key}", StringComparison.Ordinal));
        Assert.Contains(key, w => w.Key == "read" && w.Text.Contains("{n}", StringComparison.Ordinal) && w.Text.Contains("{all}", StringComparison.Ordinal));
    }

    // The tour in another language (docs/DESIGN.md §8, "Texts"): docs/tour.<code>.md beside docs/tour.md, chosen by the
    // language in use, the English tour where a language has none or its translation has fallen behind.
    [Fact]
    public void A_translation_is_shown_only_with_the_english_tours_chapters()
    {
        var english = Tour.Parse(Sample);
        var translated = Tour.Parse("""
            ## safe
            WAS ES LIEST
            Liest die Logs des Spiels.
            - reads · Die Logs des Spiels
            - never · Fasst das Spiel an

            ## next · PlanRows | MapPicker, Map
            NÄCHSTER RAID
            Karten nach dem, was deine Quests dort tun können.

            ## know · HelpButton
            GUT ZU WISSEN
            - HelpButton · HILFE · F1
            """);
        Assert.Same(translated, Tour.InLanguage(english, translated));
        Assert.Same(english, Tour.InLanguage(english, null));
        // Another part framed, a chapter missing, or a stage's key translated: the English tour.
        Assert.Same(english, Tour.InLanguage(english, Tour.Parse(Sample.Replace("PlanRows | MapPicker", "PlanRows", StringComparison.Ordinal))));
        Assert.Same(english, Tour.InLanguage(english, translated.Take(2).ToList()));
        Assert.Same(english, Tour.InLanguage(english, Tour.Parse(Sample.Replace("- never ·", "- nie ·", StringComparison.Ordinal))));
        Assert.Equal("tour.de.md", BuiltDocs.TranslationOf(BuiltDocs.Tour, "de"));
        Assert.Null(BuiltDocs.TranslationOf(BuiltDocs.Tour, "en"));
    }

    // Each translated tour in docs: a language Shturmap knows, exactly the English tour's chapters and the parts they
    // frame (the x:Names), the same keys for the words the app finds by key, the placeholders those words fill in, and
    // the band's limits (docs/tour.de.md, and each one after it).
    [Fact]
    public void Each_translated_tour_has_the_english_tours_chapters_and_anchors()
    {
        var root = RepositoryRoot();
        var english = Tour.Parse(File.ReadAllText(Path.Combine(root, "docs", "tour.md")));
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "docs"), "tour.*.md"))
        {
            var name = Path.GetFileName(file);
            var language = name["tour.".Length..^".md".Length];
            Assert.True(UiLanguage.IsWritten(language), $"{name}: '{language}' is neither offered nor being translated (UiLanguage)");
            var translated = Tour.Parse(File.ReadAllText(file));
            Assert.Equal(english.Select(c => c.Stage), translated.Select(c => c.Stage));
            foreach (var (en, tr) in english.Zip(translated))
            {
                Assert.True(en.Anchors.Select(a => string.Join("|", a)).SequenceEqual(tr.Anchors.Select(a => string.Join("|", a))),
                    $"{name}, {tr.Stage}: frames {string.Join(", ", tr.Anchors.Select(a => string.Join("|", a)))}, the English tour {string.Join(", ", en.Anchors.Select(a => string.Join("|", a)))}");
                Assert.True(tr.Title.Length <= 24 && tr.Title == tr.Title.ToUpperInvariant(), $"{name}, {tr.Stage}: the title '{tr.Title}' is no band title");
                Assert.InRange(tr.Lines.Count, 1, Tour.MaxLines);
                Assert.All(tr.Lines, line => Assert.True(line.Length <= 100, $"{name}, {tr.Stage}: '{line}' is longer than a line of the band"));
                Assert.Equal(en.Words.Count, tr.Words.Count);
                if (!Tour.KeyedStages.Contains(en.Stage))
                    continue;
                Assert.Equal(en.Words.Select(w => w.Key), tr.Words.Select(w => w.Key));
                foreach (var (enWord, trWord) in en.Words.Zip(tr.Words))
                {
                    Assert.True(TextFormat.Problem(trWord.Text) is null, $"{name}, {tr.Stage}: '{trWord.Text}' doesn't parse: {TextFormat.Problem(trWord.Text)}");
                    var expected = TextFormat.Arguments(enWord.Text)!.Order(StringComparer.Ordinal).ToList();
                    var actual = TextFormat.Arguments(trWord.Text)!.Order(StringComparer.Ordinal).ToList();
                    Assert.True(expected.SequenceEqual(actual),
                        $"{name}, {tr.Stage}, {trWord.Key}: the placeholders {{{string.Join("}, {", actual)}}}, the English word's {{{string.Join("}, {", expected)}}}");
                }
            }
            Assert.True(Tour.SameFrame(english, translated), $"{name} isn't shown: its chapters aren't the English tour's");
        }
    }

    // The words a stage fills in are texts like the app's own: placeholders in TextFormat's syntax, so a translation can
    // give {all} its plural forms.
    [Fact]
    public void The_extract_lists_words_fill_in_as_texts_do()
    {
        var key = Tour.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "tour.md"))).Single(c => c.Stage == "key").Words;
        Assert.Equal("IN THE GAME: O TWICE, THEN PRTSC", TextFormat.Format(UiLanguage.CultureFor(UiLanguage.English), key.Single(w => w.Key == "list").Text, ("key", "PRTSC")));
        Assert.Equal("YOUR LIST THIS RAID: 6 OF 9 EXTRACTS",
            TextFormat.Format(UiLanguage.CultureFor(UiLanguage.English), key.Single(w => w.Key == "read").Text, ("n", 6), ("all", 9)));
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Shturmap.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
