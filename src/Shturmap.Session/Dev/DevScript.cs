#if DEVTOOLS
using System.Globalization;
using System.Text;

namespace Shturmap.Session.Dev;

/// <summary>One step of a developer script: the verb and its arguments, with its line for errors.</summary>
public sealed record DevStep(int Line, string Verb, IReadOnlyList<string> Args)
{
    public string Arg(int i, string fallback = "") => i < Args.Count ? Args[i] : fallback;

    public double Number(int i, double fallback = 0) =>
        i < Args.Count && double.TryParse(Args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public override string ToString() => Args.Count == 0 ? Verb : $"{Verb} {string.Join(' ', Args)}";
}

/// <summary>
/// "--dev-script &lt;file&gt;": the developer view's buttons, one per line, played in order after the session has
/// started, so a dev raid can be replayed headless and snapshotted (docs/DESIGN.md §8, "Developer aids"). A line is a
/// verb and its arguments, separated by spaces ("quotes" keep spaces); "#" starts a comment. A step is followed by
/// 0.3 s for the app to read what it wrote, but the pointer's steps and "wait" (<see cref="FollowsAtOnce"/>). The
/// file's path and a snapshot's folder are read from the app's working folder, %USERPROFILE% (Program): give them
/// whole, a folder with %VARIABLES% if it likes (<see cref="Folder"/>). A session of "--dev-view" starts on settings
/// of its own, as at a first start, so the tour opens by itself once the data is there: "tour end" ends it.
/// </summary>
public static class DevScript
{
    /// <summary>The verbs and what they take; the developer view's buttons do the same.</summary>
    public static IReadOnlyDictionary<string, string> Verbs { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["mode"] = "pve | pvp | seasonal: the Session mode line",
        ["map"] = "<normalized name> [look]: the map the next raid commands use, shown in the app; with look, only shown, as a pick in the MAP list (in a raid, a look at another map)",
        ["side"] = "pmc | scav",
        ["hosting"] = "server | local",
        ["group"] = "the leader picks the current map",
        ["load"] = "loading starts: scene line, and match setup (server) or transit line (local)",
        ["steps"] = "the loading steps up to the spawn",
        ["start"] = "the raid starts",
        ["end"] = "the raid ends",
        ["cancel"] = "matching cancelled",
        ["transit"] = "<normalized name>: a transit to that map, during a raid",
        ["quest"] = "start | complete | fail <quest id or part of its name>; here [<n>]: starts the first n (8) of the shown map's quests by level",
        ["place"] = "<fx> <fy> [<to fx> <to fy>]: a click (or drag, for the facing) on the map at fractions of its size",
        ["pick"] = "<id or name>: picks (or unpicks) the quest for the coming raid, as its pen does",
        ["trader"] = "[<trader's name or id>]: holds that trader's focus, as a click on its portrait in the strip under the MAP list does; alone, lets go of it",
        ["tick"] = "<quest id or name> <n>: ticks (or unticks) the quest's n-th objective as done, as the box on its card does; not saved",
        ["show"] = "<part of a quest's name> [at row]: its card held and the first thing it needs beside it, as --show-quest does for snapshots; "
            + "with at row, its card held at the quest's own row in the rail, as a click on the row holds it (a row near the window's foot: where its card opens)",
        ["focus"] = "help | feedback | settings | map | rail | <x:Name>: the keyboard focus on that control (in the map or the rail, the first that takes it), "
            + "as Tab moves it there, so a snapshot shows its focus ring",
        ["hover"] = "[<quest id or name> [<n> | cell | key]]: the pointer on the quest's block in the lists, and in it on its n-th objective's line, its first need cell or a gold line that is a key, through the code the pointer's events call; alone, it leaves the innermost of them",
        ["point"] = "[<quest id or name> [<n>] | item <id or name>]: points at the quest, at its n-th objective or at an item, as the pointer on its line would; alone, at nothing again",
        ["trail"] = "<x> <y> [<x> <y> ...]: where the pointer has been in the main window, for the cards to tell where it is heading; no pointer moves",
        ["cards"] = "writes the open cards' titles to the app log, with its time",
        ["pos"] = "<x> <y> <z> [yaw]: a screenshot at that world position",
        ["exits"] = "<name> [<name> ...] | all: the game's extract list as a screenshot taken now shows it, read from where text recognition hands it over (\"quoted\" names with spaces; a name ending in ? is marked ??:??:??); all: every extract of the raid's map for the side",
        ["repeat"] = "a screenshot at the last position again",
        ["age"] = "<minutes>: the last position becomes older",
        ["walk"] = "<seconds between>: a screenshot at each place picked for the path",
        ["trigger"] = "report | crash | update | offline | 404 | 503 | nogame | reload (nogame: as if no game were on this PC)",
        ["choose"] = "<folder> | game | auto: \"Choose game folder…\" with that folder; game is this view's fake game; auto is FIND AUTOMATICALLY",
        ["whatsnew"] = "[<version>] [<n>] | [<version>] click <n> | leave: What's New of the newest version, as help's link shows it, or of that version (0.4.0), its card alone; "
            + "with n, the pointer on its n-th line (its preview; a line about the tour previews nothing, as for the pointer); "
            + "click n, the pointer onto line n and a tap there, through the code the pointer's events call (a line about the tour opens it); "
            + "leave, the pointer off the line it is on, as leaving the card does",
        ["replay"] = "the made-up raid of What's New's preview replayed on Customs, as at a raid's end",
        ["stash"] = "the SEEN IN YOUR STASH cue with the example items of What's New's preview, as a reading shows it",
        ["desk"] = "[<n>] | open <region> | point|pin|copy <region> [<i> <j> | <slot>] | unpin [<region> <i> <j> | all] | pick <region> <i> <j> | <slot> <n> "
            + "| rule match|under:<n>|over:<n> | plan | show [<n>]: the made-up stash desk's n-th reading as a reading in the menus brings it "
            + "(1, the first, every region open; 2, one that changed the backpack, the rest folded; 3, one that changed nothing); open, a folded region's name clicked; "
            + "point, the pointer on a region's cell at that grid place or slot (alone, its first cell: a slot, else its top left item), as the pointer's events do; "
            + "pin, the cell right-clicked (its card pinned in the rail, or unpinned); unpin, a pinned card's pin clicked, all of them with all or nothing; "
            + "copy, the cell clicked (its flash, decode and notice; the clipboard left alone in a script); pick, the n-th candidate (from 1) clicked on a \"?\" cell's card; "
            + "rule, the copy's rule as the card's − and + set it; plan and show, PLAN and DESK at the rail's head; "
            + "show n, the n-th pinned line in the rail's foot clicked, from the top (its card whole in the pointed card's place)",
        ["tasks"] = "[<n>] [ended]: the Tasks screen's OPERATIONAL table as a screenshot taken now shows it, n made-up tasks (3; at most 8: "
            + "a survival on Streets ending in 25 min, a kill on Customs, a weekly's hand-overs on any map, a kill without a countdown on Shoreline, "
            + "a line of no known kind on Woods, a survival on Interchange, a kill on Lighthouse, a weekly's kill on Reserve; 7 or 8 bring the cue's +N); "
            + "with ended, one more that runs out 3 s later",
        ["tour"] = "end: ends the tour as Esc does (a --dev-view session starts as a first start, and the tour opens by itself)",
        ["textsize"] = "normal | larger | largest: settings' Text size, chosen as its row chooses it and saved in the session's settings; the window's text, "
            + "the map and the smallest window follow at once (a run with --text-scale draws at that factor whatever the step)",
        ["language"] = "<culture>: settings' Language for this run, chosen as its row chooses it and not saved (en-US, de-DE, qps-ploc); the window's "
            + "texts, the desk's among them, follow at once, the game data in the new language a moment later",
        ["wait"] = "<seconds>",
        ["snapshot"] = "<folder>: the window and the map as PNGs, written over what the folder had; %VARIABLES% in it are expanded",
        ["exit"] = "closes the app",
        ["note"] = "<quest id or name> <text>: adds a note to the quest, as the field on its card does (the quest one word or \"quoted\"; \\n in the text breaks the line); not saved",
    };

    /// <summary>
    /// A <c>note</c> step's quest and words: the first word is the quest (its id or its name, "quoted" when the name has
    /// spaces), the rest of the line is the note, where "\n" breaks the line. Null without both.
    /// </summary>
    public static (string Quest, string Text)? Note(DevStep step) =>
        step.Args.Count < 2 ? null : (step.Args[0], string.Join(' ', step.Args.Skip(1)).Replace("\\n", "\n", StringComparison.Ordinal));

    /// <summary>
    /// An "exits" step's rows, in the order the list shows them: each name, and whether the game marks it "??:??:??" (the
    /// name ends in "?", which goes; "ZB-1013?"). Null for "exits all": every extract of the raid's map for the side,
    /// none marked. Empty without names.
    /// </summary>
    public static IReadOnlyList<(string Name, bool Marked)>? ExitsOf(DevStep step) =>
        step.Args is [var only] && only.Equals("all", StringComparison.OrdinalIgnoreCase) ? null
            : step.Args.Select(a => a.Trim()).Where(a => a.TrimEnd('?').Length > 0)
                .Select(a => a.EndsWith('?') ? (a.TrimEnd('?').TrimEnd(), true) : (a, false)).ToList();

    /// <summary>
    /// A "tasks" step's made-up operational tasks (owner, 2026-10-19, B): how many (3 without a number, at most 8, so the
    /// cue's last place can say "+N": QA pass of 2026-10-19, F3) and whether one more runs out at once ("ended"). Null for
    /// any other word.
    /// </summary>
    public static (int Count, bool Ended)? TasksOf(DevStep step)
    {
        var count = 3;
        var ended = false;
        foreach (var arg in step.Args)
        {
            if (arg.Equals("ended", StringComparison.OrdinalIgnoreCase))
                ended = true;
            else if (int.TryParse(arg, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n <= MostExampleTasks)
                count = n;
            else
                return null;
        }
        return (count, ended);
    }

    /// <summary>What a "desk" step does: shows a made-up reading, opens a folded region, points at a cell, pins, unpins,
    /// clicks (copies) or picks one, sets the copy's rule, goes back to Plan or to the desk, or clicks a pinned line in the
    /// rail's foot.</summary>
    public enum DeskAct
    {
        Reading,
        Open,
        Point,
        Plan,
        Show,
        ShowPin,
        Pin,
        Unpin,
        UnpinAll,
        Copy,
        Pick,
        Rule,
    }

    /// <summary>A "desk" step's parts: the act; the reading; the region and the cell's grid place (−1, −1 for its first
    /// cell) or slot; a pick's candidate or a pinned line (from 1); a rule ("match", "under:3", "over:2").</summary>
    public sealed record DeskStep(DeskAct Act, int Reading = 0, string Region = "", int I = 0, int J = 0)
    {
        public string? Slot { get; init; }

        public int N { get; init; }

        public string? Rule { get; init; }
    }

    /// <summary>
    /// A "desk" step (step B of the stash desk, 2026-10-19; step C, 2026-10-20): "desk [&lt;n&gt;]" the made-up desk's
    /// reading n (1 without a number, at most 3), "desk open &lt;region&gt;" a folded region's name clicked, "desk point
    /// &lt;region&gt; [&lt;i&gt; &lt;j&gt; | &lt;slot&gt;]" the pointer on its cell at that grid place or slot (alone, its first
    /// cell), "desk pin …" the cell right-clicked, "desk copy …" the cell clicked, "desk unpin [&lt;region&gt; &lt;i&gt; &lt;j&gt; |
    /// all]" a pin clicked (all, or nothing named: every pin), "desk pick &lt;region&gt; &lt;i&gt; &lt;j&gt; | &lt;slot&gt; &lt;n&gt;" a
    /// "?" cell's n-th candidate clicked on its card, "desk rule match | under:&lt;n&gt; | over:&lt;n&gt;" the copy's rule, "desk
    /// plan" and "desk show" PLAN and DESK clicked, "desk show &lt;n&gt;" the n-th pinned line in the rail's foot clicked, from
    /// the top (UI QA pass of the second round, F3, 2026-10-19), so its card shows whole. A region is its key (loadout, rig,
    /// pockets, special, backpack, pouch, stash) or a window's title (zhuk); a slot its key (headwear, armband). Null for
    /// anything else.
    /// </summary>
    public static DeskStep? DeskOf(DevStep step)
    {
        static bool Number(string text, out int n) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out n);
        static bool Is(string text, string word) => text.Equals(word, StringComparison.OrdinalIgnoreCase);
        static DeskAct? CellAct(string verb) => Is(verb, "point") ? DeskAct.Point : Is(verb, "pin") ? DeskAct.Pin : Is(verb, "copy") ? DeskAct.Copy
            : Is(verb, "unpin") ? DeskAct.Unpin : null;
        return step.Args switch
        {
            [] => new DeskStep(DeskAct.Reading, 1),
            [var n] when Number(n, out var reading) && reading is >= 1 and <= 3 => new DeskStep(DeskAct.Reading, reading),
            [var plan] when Is(plan, "plan") => new DeskStep(DeskAct.Plan),
            [var show] when Is(show, "show") => new DeskStep(DeskAct.Show),
            [var show, var n] when Is(show, "show") && Number(n, out var line) && line >= 1 => new DeskStep(DeskAct.ShowPin) { N = line },
            [var unpin] when Is(unpin, "unpin") => new DeskStep(DeskAct.UnpinAll),
            [var unpin, var all] when Is(unpin, "unpin") && Is(all, "all") => new DeskStep(DeskAct.UnpinAll),
            [var open, var region] when Is(open, "open") => new DeskStep(DeskAct.Open, 0, region.ToLowerInvariant()),
            [var rule, var said] when Is(rule, "rule") && Core.Desk.DeskCopyRule.Parse(said) is { } parsed => new DeskStep(DeskAct.Rule) { Rule = parsed.Format() },
            [var verb, var region] when CellAct(verb) is { } act && act != DeskAct.Unpin => new DeskStep(act, 0, region.ToLowerInvariant(), -1, -1),
            [var verb, var region, var slot] when CellAct(verb) is { } act && !Number(slot, out _) => new DeskStep(act, 0, region.ToLowerInvariant()) { Slot = slot.ToLowerInvariant() },
            [var verb, var region, var i, var j] when CellAct(verb) is { } act && Number(i, out var x) && Number(j, out var y)
                => new DeskStep(act, 0, region.ToLowerInvariant(), x, y),
            [var pick, var region, var slot, var n] when Is(pick, "pick") && !Number(slot, out _) && Number(n, out var nth) && nth >= 1
                => new DeskStep(DeskAct.Pick, 0, region.ToLowerInvariant()) { Slot = slot.ToLowerInvariant(), N = nth },
            [var pick, var region, var i, var j, var n] when Is(pick, "pick") && Number(i, out var x) && Number(j, out var y) && Number(n, out var nth) && nth >= 1
                => new DeskStep(DeskAct.Pick, 0, region.ToLowerInvariant(), x, y) { N = nth },
            _ => null,
        };
    }

    /// <summary>The most made-up tasks a "tasks" step takes: the session's table has eight (GameSession.ReadExampleTasksAsync).</summary>
    public const int MostExampleTasks = 8;

    /// <summary>The steps of settings' Text size a "textsize" step takes, as shturmap.db keeps them (Rules.TextSize).</summary>
    public static IReadOnlyList<string> TextSizes { get; } = ["normal", "larger", "largest"];

    /// <summary>
    /// A "textsize" step's step of settings' Text size (QA pass of 2026-10-19, F4: no step chose one live, so the
    /// setting's change while Shturmap runs couldn't be pictured), lower case; null for anything else.
    /// </summary>
    public static string? TextSizeOf(DevStep step) =>
        step.Args is [var only] && TextSizes.FirstOrDefault(t => t.Equals(only, StringComparison.OrdinalIgnoreCase)) is { } size ? size : null;

    /// <summary>
    /// A "language" step's culture, as --culture takes it ("de-DE", "en", "qps-ploc"; release QA pass of 2026-10-19, F1: no
    /// step switched the language while a desk's cards were up, so the switch couldn't be pictured with them); null for
    /// anything that isn't one culture's name. Whether Windows knows it, the window says.
    /// </summary>
    public static string? LanguageOf(DevStep step) =>
        step.Args is [var only] && System.Text.RegularExpressions.Regex.IsMatch(only, "^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$") ? only : null;

    /// <summary>What a "whatsnew" step does with the card: the pointer onto a line (or, with none, off the card), a tap
    /// on a line, or the pointer off the line it is on.</summary>
    public enum WhatsNewAct
    {
        Point,
        Click,
        Leave,
    }

    /// <summary>
    /// "whatsnew [&lt;n&gt;]", "whatsnew click &lt;n&gt;", "whatsnew leave", each after a version if it names one
    /// (<see cref="WhatsNewVersionOf"/>): what the step does, and the line it is about, counted from 1 (0: none). Null
    /// for a click without a line.
    /// </summary>
    public static (WhatsNewAct Act, int Line)? WhatsNewOf(DevStep step)
    {
        var at = WhatsNewVersionOf(step) is null ? 0 : 1;
        return step.Arg(at).ToLowerInvariant() switch
        {
            "click" => step.Number(at + 1) >= 1 ? (WhatsNewAct.Click, (int)step.Number(at + 1)) : null,
            "leave" => (WhatsNewAct.Leave, 0),
            _ => (WhatsNewAct.Point, (int)step.Number(at, 0)),
        };
    }

    /// <summary>
    /// The version a "whatsnew" step names before what it does ("whatsnew 0.4.0 2"; owner, 2026-10-15, D10: a script
    /// reaches an older version's lines, 0.4.0's extracts preview among them), as docs/whats-new.md heads its section;
    /// null for the newest version's card. A version has a dot, so a line's number is never taken for one.
    /// </summary>
    public static string? WhatsNewVersionOf(DevStep step) =>
        step.Args.Count > 0 && step.Args[0].Contains('.', StringComparison.Ordinal) && Version.TryParse(step.Args[0], out _) ? step.Args[0] : null;

    /// <summary>
    /// A "show" step's quest, and whether its card is held at the quest's own row ("show Dandies at row"; owner,
    /// 2026-10-15, D10), as a click on the row holds it, rather than where --show-quest puts it. Null without a quest.
    /// </summary>
    public static (string Quest, bool AtRow)? ShowOf(DevStep step)
    {
        var atRow = step.Args.Count > 2 && step.Args[^2].Equals("at", StringComparison.OrdinalIgnoreCase)
            && step.Args[^1].Equals("row", StringComparison.OrdinalIgnoreCase);
        var quest = string.Join(' ', atRow ? step.Args.Take(step.Args.Count - 2) : step.Args);
        return quest.Length == 0 ? null : (quest, atRow);
    }

    /// <summary>The words a "focus" step knows, and the x:Name in MainWindow.xaml each stands for; any other word is
    /// taken for an x:Name itself.</summary>
    public static IReadOnlyDictionary<string, string> FocusParts { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["help"] = "HelpButton",
        ["feedback"] = "FeedbackButton",
        ["settings"] = "SettingsButton",
        ["map"] = "Map",
        ["rail"] = "Rail",
    };

    /// <summary>
    /// The control a "focus" step moves the keyboard focus to, by its x:Name (QA pass, 2026-10-15: no step could focus a
    /// status bar button, so no still showed a focus ring). Null without one.
    /// </summary>
    public static string? FocusOf(DevStep step) =>
        step.Args.Count != 1 ? null : FocusParts.GetValueOrDefault(step.Args[0]) ?? step.Args[0];

    /// <summary>
    /// Whether the next step follows at once. The pointer's steps (hover, point, trail, cards, and What's New's click
    /// and leave) and "wait" write nothing for the app to read, and what they check is a matter of tenths of a second;
    /// after every other step the app is given 0.3 s, as it reads the logs about twice a second.
    /// </summary>
    public static bool FollowsAtOnce(DevStep step) =>
        step.Verb is "hover" or "point" or "trail" or "cards" or "wait"
        || step.Verb == "whatsnew" && WhatsNewOf(step) is { Act: WhatsNewAct.Click or WhatsNewAct.Leave };

    /// <summary>A folder a step names, with its %VARIABLES% expanded: a script kept in the repository names no user's
    /// folder ("%TEMP%\shturmap-qa\t0"). One left relative is in the app's working folder, %USERPROFILE%.</summary>
    public static string Folder(string folder) => Environment.ExpandEnvironmentVariables(folder);

    public static (IReadOnlyList<DevStep> Steps, IReadOnlyList<string> Errors) Parse(string text)
    {
        var steps = new List<DevStep>();
        var errors = new List<string>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hash = line.IndexOf('#');
            if (hash >= 0)
                line = line[..hash];
            var words = Words(line);
            if (words.Count == 0)
                continue;
            var verb = words[0].ToLowerInvariant();
            if (!Verbs.ContainsKey(verb))
            {
                errors.Add($"line {i + 1}: unknown step '{words[0]}'");
                continue;
            }
            var step = new DevStep(i + 1, verb, words.Skip(1).ToList());
            if (verb == "whatsnew" && WhatsNewOf(step) is null)
            {
                errors.Add($"line {i + 1}: 'whatsnew click' needs the line's number, from 1");
                continue;
            }
            if (verb == "tasks" && TasksOf(step) is null)
            {
                errors.Add($"line {i + 1}: 'tasks' takes how many (0 to {MostExampleTasks}) and 'ended'");
                continue;
            }
            if (verb == "desk" && DeskOf(step) is null)
            {
                errors.Add($"line {i + 1}: 'desk' takes a reading (1 to 3), 'open <region>', 'point|pin|copy <region> [<i> <j> | <slot>]', 'unpin [<region> <i> <j> | all]', "
                    + "'pick <region> <i> <j> | <slot> <n>', 'rule match|under:<n>|over:<n>', 'plan' or 'show [<n>]'");
                continue;
            }
            if (verb == "textsize" && TextSizeOf(step) is null)
            {
                errors.Add($"line {i + 1}: 'textsize' takes normal, larger or largest");
                continue;
            }
            if (verb == "language" && LanguageOf(step) is null)
            {
                errors.Add($"line {i + 1}: 'language' takes a culture (en-US, de-DE, qps-ploc)");
                continue;
            }
            if (verb == "tour" && !step.Arg(0).Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"line {i + 1}: 'tour' takes 'end'");
                continue;
            }
            if (verb == "exits" && ExitsOf(step) is { Count: 0 })
            {
                errors.Add($"line {i + 1}: 'exits' needs the extracts' names, or 'all'");
                continue;
            }
            if (verb == "show" && ShowOf(step) is null)
            {
                errors.Add($"line {i + 1}: 'show' needs a quest's name");
                continue;
            }
            if (verb == "focus" && FocusOf(step) is null)
            {
                errors.Add($"line {i + 1}: 'focus' takes one of help, feedback, settings, map, rail or a control's x:Name");
                continue;
            }
            steps.Add(step);
        }
        return (steps, errors);
    }

    // Space-separated words; "double quotes" keep spaces inside a word.
    private static List<string> Words(string line)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        var quoted = false;
        var any = false;
        foreach (var ch in line)
        {
            if (ch == '"')
            {
                quoted = !quoted;
                any = true;
            }
            else if (char.IsWhiteSpace(ch) && !quoted)
            {
                if (any)
                    words.Add(word.ToString());
                word.Clear();
                any = false;
            }
            else
            {
                word.Append(ch);
                any = true;
            }
        }
        if (any)
            words.Add(word.ToString());
        return words;
    }
}
#endif
