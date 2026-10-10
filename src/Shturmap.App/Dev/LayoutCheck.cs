#if DEVTOOLS
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Controls;
using Shturmap.App.Rules;
using SkiaSharp;
using Rect = Windows.Foundation.Rect;
using Size = Windows.Foundation.Size;

namespace Shturmap.App.Dev;

/// <summary>
/// The layout check (docs/LANGUAGES.md, "Layout check"; developer builds only). For each picture a snapshot saves of
/// the window or one of its popups (window.png, tour.png, help.png, settings.png, card.png), it walks the elements the
/// picture shows and writes beside it what a longer language would break:
/// <list type="bullet">
/// <item>"&lt;name&gt;.texts.txt": every text shown, one per line, in the visual tree's order: a TextBlock's or
/// RichTextBlock's text (its runs together; a button's or link's words are TextBlocks too), then "tooltip: …",
/// "name: …" (the screen reader's) and "input: …" (a text box's own text). Shown means not collapsed, not transparent and
/// not of no size. Texts outside every viewport around them (scrolled out of a list, below a popup's fold) follow
/// under "--- not in view ---", since a later scroll shows them: they are checked like the rest.</item>
/// <item>"&lt;name&gt;.layout.json": the problems, numbered, each with its kind, element, text and bounds in window
/// pixels. <b>trimmed</b>: a text that ends in "…" where the design doesn't let it (<see cref="Fit.MayTrim"/> marks where
/// it does). <b>overflow</b>: a text or control that sticks out of the nearest element that clips it (a scrolling list
/// counts its whole extent, not its viewport) or out of the window, a text wider or taller than its own box (cut
/// without "…"), and a word too long for its line (it breaks inside the word). <b>overlap</b>: two texts over each
/// other, except within deliberate layers (<see cref="Layers"/>). <b>untranslated</b>, in the pseudo-language only: a
/// text, or part of one, that isn't the pseudo-language's, nor a game name, a number with its unit, a key or a word on
/// <see cref="LayoutWords.Neutral"/>'s short list.</item>
/// <item>"&lt;name&gt;.problems.png": the picture with each problem boxed and numbered as in the JSON (written only
/// when there is one).</item>
/// </list>
/// The map's own drawing (labels on the map) is a picture, not text elements, and isn't checked here.
/// </summary>
internal sealed class LayoutCheck
{
    /// <summary>The kinds of problem, in the order they are numbered.</summary>
    public static readonly string[] Kinds = ["overflow", "trimmed", "overlap", "untranslated"];

    /// <summary>
    /// Elements that lie over the rest of the window on purpose: the tour's dim and band, the Report dialog, a cue's
    /// band, What's New's plate. Their texts are compared with each other, not with what they cover.
    /// </summary>
    public static readonly IReadOnlySet<string> Layers = new HashSet<string>(StringComparer.Ordinal)
    {
        "TourLayer", "ReportOverlay", "CuePanel", "WhatsNewPlate",
    };

    // How far a box may stick out, in DIP: layout rounding and a hairline's negative margin.
    private const double Slack = 1.0;

    private readonly UIElement _picture;
    private readonly Rect _window;
    private readonly double _scale;
    private readonly Context _context;
    private readonly List<Shown> _shown = [];
    private readonly List<Problem> _problems = [];
    private readonly HashSet<FrameworkElement> _overflowing = [];
    // How many elements lie above the picture's own (a popup's frame, the window's): names are looked for below them.
    private int _rootDepth;

    /// <summary>What the check needs to know of the run.</summary>
    /// <param name="Pseudo">The pseudo-language is in use: look for untranslated texts.</param>
    /// <param name="Language">The language in use ("en", "de", "qps-ploc").</param>
    /// <param name="Culture">The culture texts are formatted in and the thread's ("en-US", "de-DE").</param>
    /// <param name="Names">Names that stay as they are in every language: the game data's (in <see cref="Key"/>'s
    /// form) and the releases' names.</param>
    /// <param name="OuterWindow">The window's outer size in pixels, title bar and frame included.</param>
    public sealed record Context(bool Pseudo, string Language, string Culture, IReadOnlySet<string> Names, (int Width, int Height) OuterWindow);

    private LayoutCheck(UIElement picture, Context context)
    {
        _picture = picture;
        _context = context;
        var root = picture.XamlRoot;
        _window = new Rect(0, 0, root.Size.Width, root.Size.Height);
        _scale = root.RasterizationScale;
    }

    /// <summary>
    /// Checks the element a picture shows and writes its texts, its problems and the picture with them boxed beside
    /// <paramref name="png"/>. <paramref name="pixels"/> is the picture itself (BGRA, premultiplied), as rendered.
    /// </summary>
    public static async Task RunAsync(UIElement picture, string png, byte[] pixels, int width, int height, Context context)
    {
        var check = new LayoutCheck(picture, context);
        check.Walk();
        var origin = check.BoundsOf(picture) ?? new Rect(0, 0, width, height);
        var toPicture = origin.Width > 0 ? width / origin.Width : check._scale;
        var stem = Path.Combine(Path.GetDirectoryName(png) ?? ".", Path.GetFileNameWithoutExtension(png));
        var texts = check.TextsFile();
        // The JSON numbers the problems, and the picture's boxes carry the same numbers.
        var json = check.Json(Path.GetFileName(png));
        var boxes = check._problems.Select(p => p.Boxes(origin, toPicture)).ToList();
        await Task.Run(() =>
        {
            File.WriteAllText(stem + ".texts.txt", texts, new UTF8Encoding(false));
            File.WriteAllText(stem + ".layout.json", json, new UTF8Encoding(false));
            var problemsPng = stem + ".problems.png";
            if (check._problems.Count > 0)
                DrawProblems(pixels, width, height, check._problems, boxes, toPicture, problemsPng);
            else if (File.Exists(problemsPng))
                File.Delete(problemsPng);
        });
    }

    // ---- the walk ----

    /// <summary>A text shown, as it goes into the texts file.</summary>
    /// <param name="Clips">What clips it, outermost first: two texts overlap only where both can be seen.</param>
    private sealed record Shown(FrameworkElement Element, string Kind, string Text, Rect Box, Rect Ink, bool InView, DependencyObject[] Chain, Clip[] Clips);

    /// <summary>An element that clips what is inside it.</summary>
    /// <param name="View">What of its content is on screen.</param>
    /// <param name="Room">What its content must fit in: a scrolling list's whole extent, else <paramref name="View"/>.</param>
    private sealed record Clip(Rect View, Rect Room, string Name, bool Scrolls);

    private void Walk()
    {
        // What clips the picture from outside: a popup's own scrolling (help and settings scroll in their flyout),
        // and the window around everything.
        var outer = new List<DependencyObject>();
        for (var up = VisualTreeHelper.GetParent(_picture); up is not null; up = VisualTreeHelper.GetParent(up))
            outer.Insert(0, up);
        var clips = new List<Clip> { new(_window, _window, "the window", false) };
        var chain = new List<DependencyObject>();
        foreach (var element in outer)
        {
            chain.Add(element);
            if (element is FrameworkElement fe && BoundsOf(fe) is { } box)
                clips.AddRange(ClipsOf(fe, box, chain));
        }
        _rootDepth = chain.Count;
        Walk(_picture, chain, clips, inScrollBar: false);
        FindOverlaps();
        if (_context.Pseudo)
            FindUntranslated();
    }

    private void Walk(DependencyObject node, List<DependencyObject> chain, List<Clip> clips, bool inScrollBar)
    {
        if (node is UIElement { Visibility: Visibility.Collapsed } or UIElement { Opacity: <= 0.01 })
            return;
        chain.Add(node);
        var added = 0;
        if (node is FrameworkElement fe && BoundsOf(fe) is { } box)
        {
            // An element's own clip (its explicit one, or its slot when it was given less room than it took) cuts it too.
            foreach (var clip in ClipsOf(fe, box, chain))
            {
                clips.Add(clip);
                added++;
            }
            if (box.Width > 0 && box.Height > 0)
                Visit(fe, box, chain, clips, inScrollBar);
        }
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
            Walk(VisualTreeHelper.GetChild(node, i), chain, clips, inScrollBar || node is ScrollBar);
        clips.RemoveRange(clips.Count - added, added);
        chain.RemoveAt(chain.Count - 1);
    }

    private IEnumerable<Clip> ClipsOf(FrameworkElement fe, Rect box, IReadOnlyList<DependencyObject> chain)
    {
        var name = Describe(fe, chain);
        // A scrolling list shows its viewport; what is in it must fit its whole extent.
        if (fe is ScrollContentPresenter)
        {
            var room = box;
            if (chain.OfType<ScrollViewer>().LastOrDefault() is { } scroll)
            {
                room = Union(box, new Rect(box.X - scroll.HorizontalOffset, box.Y - scroll.VerticalOffset,
                    scroll.ExtentWidth * scroll.ZoomFactor, scroll.ExtentHeight * scroll.ZoomFactor));
                name = Describe(scroll, chain);
            }
            yield return new Clip(box, room, name, true);
        }
        // A scrolling list's own clip is its viewport, the clip above.
        else if (fe.Clip is RectangleGeometry geometry && Transform(fe, geometry.Transform is { } t ? t.TransformBounds(geometry.Rect) : geometry.Rect) is { } clip)
            yield return new Clip(clip, clip, name, false);
        // Given less room than it took, an element is cut at its layout slot.
        if (VisualTreeHelper.GetParent(fe) is UIElement parent)
        {
            var slot = LayoutInformation.GetLayoutSlot(fe);
            var m = fe.Margin;
            var inner = new Rect(slot.X + m.Left, slot.Y + m.Top, Math.Max(0, slot.Width - m.Left - m.Right), Math.Max(0, slot.Height - m.Top - m.Bottom));
            if ((fe.ActualWidth > inner.Width + Slack || fe.ActualHeight > inner.Height + Slack) && Transform(parent, inner) is { } cut)
                yield return new Clip(cut, cut, name + "'s slot", false);
        }
    }

    private void Visit(FrameworkElement fe, Rect box, List<DependencyObject> chain, List<Clip> clips, bool inScrollBar)
    {
        var inView = clips.Aggregate(box, (r, c) => Intersect(r, c.View)) is { Width: > 0.5, Height: > 0.5 };
        var ink = box;
        switch (fe)
        {
            case TextBlock tb when Readable(TextOf(tb)) is { } text:
                ink = CheckText(tb, text, box, chain, clips, inView);
                Add(fe, "text", text, box, ink, inView, chain, clips);
                break;
            case RichTextBlock rtb when Readable(TextOf(rtb)) is { } text:
                if (rtb.TextTrimming != TextTrimming.None && rtb.IsTextTrimmed && !Fit.GetMayTrim(rtb))
                    Report("trimmed", fe, chain, text, box, inView, "ends in \"…\" where the design doesn't let it trim (Fit.MayTrim)");
                else if (rtb.HasOverflowContent)
                    Report("overflow", fe, chain, text, box, inView, "its text goes on past its box and is cut off");
                else
                    CheckRoom(fe, box, text, chain, clips, inView);
                Add(fe, "text", text, box, ink, inView, chain, clips);
                break;
            case TextBox textBox:
                if (Readable(textBox.Text) is { } input)
                    Add(fe, "input", input, box, box, inView, chain, clips);
                CheckRoom(fe, box, LabelOf(fe), chain, clips, inView);
                break;
            case ButtonBase or ComboBox when !inScrollBar:
                CheckRoom(fe, box, LabelOf(fe), chain, clips, inView);
                break;
        }
        if (Readable(TipOf(ToolTipService.GetToolTip(fe))) is { } tip)
            Add(fe, "tooltip", tip, box, box, inView, chain, clips);
        if (Readable(AutomationProperties.GetName(fe)) is { } spoken)
            Add(fe, "name", spoken, box, box, inView, chain, clips);
    }

    private void Add(FrameworkElement fe, string kind, string text, Rect box, Rect ink, bool inView, List<DependencyObject> chain, List<Clip> clips) =>
        _shown.Add(new Shown(fe, kind, text, box, ink, inView, [.. chain], [.. clips]));

    // What a control says, for its problem: its screen reader's name, else the words in it, else its tooltip.
    private static string LabelOf(FrameworkElement control)
    {
        if (Readable(AutomationProperties.GetName(control)) is { } name)
            return name;
        var words = new List<string>();
        var pending = new Stack<DependencyObject>();
        pending.Push(control);
        while (pending.Count > 0 && words.Count < 3)
        {
            var node = pending.Pop();
            if (node is TextBlock tb && Readable(TextOf(tb)) is { } text)
                words.Add(text);
            for (var i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
                pending.Push(VisualTreeHelper.GetChild(node, i));
        }
        return words.Count > 0 ? string.Join(" ", words) : Readable(TipOf(ToolTipService.GetToolTip(control))) ?? "";
    }

    // A text: cut off with "…" where it may not be, wider or taller than its box (cut without "…"), a word too long for
    // its line, or sticking out of what clips it. Returns where its letters are (its box where they fit).
    private Rect CheckText(TextBlock tb, string text, Rect box, List<DependencyObject> chain, List<Clip> clips, bool inView)
    {
        var mayTrim = Fit.GetMayTrim(tb);
        if (tb.TextTrimming != TextTrimming.None)
        {
            if (tb.IsTextTrimmed && !mayTrim)
                Report("trimmed", tb, chain, text, box, inView, "ends in \"…\" where the design doesn't let it trim (Fit.MayTrim)");
            CheckRoom(tb, box, text, chain, clips, inView);
            return box;
        }
        // The room it was given: its layout slot without its margins. A TextBlock is as wide as its words, so its own
        // box says nothing of the room around it.
        var (roomWidth, roomHeight) = RoomOf(tb);
        var wraps = tb.TextWrapping != TextWrapping.NoWrap;
        // A hair more than the room: measured at exactly its own width, a line wraps its last word.
        var natural = Measured(tb, wraps ? roomWidth + 0.5 : double.PositiveInfinity);
        var ink = InkOf(tb, box, natural);
        if (natural.Width > roomWidth + Slack)
        {
            Report("overflow", tb, chain, text, ink, inView,
                $"the text needs {natural.Width:0} DIP of width and has {roomWidth:0}: cut off without \"…\"");
            return ink;
        }
        if (natural.Height > roomHeight + Slack)
        {
            Report("overflow", tb, chain, text, ink, inView,
                $"the text needs {natural.Height:0} DIP of height and has {roomHeight:0}: lines cut off without \"…\"");
            return ink;
        }
        if (wraps && LongestWord(tb, text, roomWidth) is { } word)
        {
            Report("overflow", tb, chain, text, box, inView, $"the word \"{word}\" is wider than its line ({roomWidth:0} DIP), so it breaks inside the word");
            return ink;
        }
        CheckRoom(tb, ink, text, chain, clips, inView);
        return ink;
    }

    // Sticks out of the nearest element that clips it, or out of the window (where no scrolling list is around it).
    private void CheckRoom(FrameworkElement fe, Rect rect, string text, List<DependencyObject> chain, List<Clip> clips, bool inView)
    {
        // A control that sticks out already says so for the texts in it.
        if (chain.OfType<FrameworkElement>().Any(e => !ReferenceEquals(e, fe) && _overflowing.Contains(e)))
            return;
        var nearest = clips[^1];
        string? outOf = null;
        if (!Contains(nearest.Room, rect))
            outOf = nearest.Name;
        else if (!clips.Any(c => c.Scrolls) && !Contains(_window, rect))
            outOf = "the window";
        if (outOf is null)
            return;
        _overflowing.Add(fe);
        var room = outOf == "the window" ? _window : nearest.Room;
        Report("overflow", fe, chain, text, rect, inView, $"sticks out of {outOf} by {Overhang(rect, room)}");
    }

    private static string Overhang(Rect rect, Rect room)
    {
        var sides = new List<string>();
        if (rect.Left < room.Left - Slack) sides.Add($"{room.Left - rect.Left:0} left");
        if (rect.Right > room.Right + Slack) sides.Add($"{rect.Right - room.Right:0} right");
        if (rect.Top < room.Top - Slack) sides.Add($"{room.Top - rect.Top:0} top");
        if (rect.Bottom > room.Bottom + Slack) sides.Add($"{rect.Bottom - room.Bottom:0} bottom");
        return sides.Count == 0 ? "a little" : string.Join(", ", sides) + " DIP";
    }

    // Two texts over each other. Each pair is looked at once, under the element both are in (siblings in a panel, or
    // further down in two of its children); texts inside a deliberate layer aren't compared with what it covers.
    private void FindOverlaps()
    {
        var texts = _shown.Where(s => s.Kind == "text" && s.Text.Any(char.IsLetterOrDigit)).ToList();
        for (var i = 0; i < texts.Count; i++)
        {
            for (var j = i + 1; j < texts.Count; j++)
            {
                var (a, b) = (texts[i], texts[j]);
                if (ReferenceEquals(a.Element, b.Element) || Intersect(Deflate(a.Ink), Deflate(b.Ink)) is not { Width: > 0, Height: > 0 })
                    continue;
                // What clips one and not the other (a scrolling list one of them is in) hides what lies outside it.
                var shared = 0;
                while (shared < a.Clips.Length && shared < b.Clips.Length && ReferenceEquals(a.Clips[shared], b.Clips[shared]))
                    shared++;
                var seenA = a.Clips.Skip(shared).Aggregate(a.Ink, (r, c) => Intersect(r, c.View));
                var seenB = b.Clips.Skip(shared).Aggregate(b.Ink, (r, c) => Intersect(r, c.View));
                if (Intersect(Deflate(seenA), Deflate(seenB)) is not { Width: > 0, Height: > 0 })
                    continue;
                var common = 0;
                while (common < a.Chain.Length && common < b.Chain.Length && ReferenceEquals(a.Chain[common], b.Chain[common]))
                    common++;
                if (a.Chain.Skip(common).Concat(b.Chain.Skip(common)).Any(e => e is FrameworkElement { Name: var n } && Layers.Contains(n)))
                    continue;
                var problem = Report("overlap", a.Element, a.Chain, a.Text, a.Ink, a.InView || b.InView,
                    $"lies over \"{Short(b.Text)}\" ({Describe(b.Element, b.Chain)})");
                problem.Other = Pixels(b.Ink);
                problem.OtherInk = b.Ink;
                problem.OtherElement = Describe(b.Element, b.Chain);
                problem.OtherText = b.Text;
            }
        }
    }

    // In the pseudo-language every text of Shturmap's own is in brackets: what is left outside them is a text that
    // isn't in the texts files yet, unless it is a game name, a number, a key or on the short list.
    private void FindUntranslated()
    {
        foreach (var shown in _shown.Where(s => s.Kind != "input"))
        {
            var english = LayoutWords.EnglishParts(shown.Text, _context.Names, CultureInfo.CurrentCulture);
            if (english.Count == 0)
                continue;
            var what = shown.Kind == "text" ? "" : shown.Kind + " ";
            Report("untranslated", shown.Element, shown.Chain, shown.Text, shown.Box, shown.InView,
                $"{what}not in the pseudo-language: \"{Short(string.Join(" … ", english))}\"");
        }
    }

    private Problem Report(string kind, FrameworkElement fe, IReadOnlyList<DependencyObject> chain, string text, Rect bounds, bool inView, string detail)
    {
        var problem = new Problem(kind, Describe(fe, chain), text, bounds, Pixels(bounds), detail, inView);
        _problems.Add(problem);
        return problem;
    }

    private sealed record Problem(string Kind, string Element, string Text, Rect Ink, int[] Bounds, string Detail, bool InView)
    {
        public int Number { get; set; }
        public int[]? Other { get; set; }
        public Rect? OtherInk { get; set; }
        public string? OtherElement { get; set; }
        public string? OtherText { get; set; }

        // Its boxes in the picture's pixels.
        public List<SKRect> Boxes(Rect origin, double toPicture) =>
            new[] { (Rect?)Ink, OtherInk }.OfType<Rect>()
                .Select(r => new SKRect((float)((r.X - origin.X) * toPicture), (float)((r.Y - origin.Y) * toPicture),
                    (float)((r.X - origin.X + r.Width) * toPicture), (float)((r.Y - origin.Y + r.Height) * toPicture)))
                .ToList();
    }

    // ---- texts ----

    private static string TextOf(TextBlock tb)
    {
        if (tb.Inlines.Count == 0)
            return tb.Text ?? "";
        var text = new StringBuilder();
        Append(text, tb.Inlines);
        return text.ToString();
    }

    private static string TextOf(RichTextBlock rtb)
    {
        var text = new StringBuilder();
        foreach (var block in rtb.Blocks.OfType<Paragraph>())
        {
            if (text.Length > 0)
                text.Append('\n');
            Append(text, block.Inlines);
        }
        return text.ToString();
    }

    private static void Append(StringBuilder text, InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    text.Append(run.Text);
                    break;
                case LineBreak:
                    text.Append('\n');
                    break;
                case Span span:
                    Append(text, span.Inlines);
                    break;
            }
        }
    }

    // A tooltip's words: a string, or the text of the element it shows.
    private static string? TipOf(object? tip) => tip switch
    {
        null => null,
        string s => s,
        ToolTip { Content: var content } => TipOf(content),
        TextBlock tb => TextOf(tb),
        ContentControl { Content: var content } => TipOf(content),
        Border { Child: var child } => TipOf(child),
        Panel panel => string.Join(" ", panel.Children.Select(TipOf).Where(t => !string.IsNullOrWhiteSpace(t))),
        _ => null,
    };

    // A text worth listing: not empty, not only a symbol font's glyphs (Segoe Fluent Icons' private-use letters).
    private static string? Readable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return text.Any(c => !char.IsWhiteSpace(c) && c is < '\uE000' or > '\uF8FF') ? text : null;
    }

    private string TextsFile()
    {
        var file = new StringBuilder();
        foreach (var shown in _shown.Where(s => s.InView))
            file.Append(Line(shown)).Append("\r\n");
        var hidden = _shown.Where(s => !s.InView).ToList();
        if (hidden.Count > 0)
        {
            file.Append("--- not in view ---\r\n");
            foreach (var shown in hidden)
                file.Append(Line(shown)).Append("\r\n");
        }
        return file.ToString();
    }

    private static string Line(Shown shown)
    {
        var text = Masked(shown.Text).Replace("\r\n", "\n").Replace("\n", "\\n").Replace('\t', ' ');
        return shown.Kind == "text" ? text : shown.Kind + ": " + text;
    }

    // The fake game's folder, and any path the app shows, lies in the user's folder: its name stays out of the files.
    private static readonly string UserFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string Masked(string text) =>
        UserFolder.Length > 3 ? text.Replace(UserFolder, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase) : text;

    private static string Short(string text)
    {
        var one = text.Replace("\n", " ");
        return one.Length <= 80 ? one : one[..79] + "…";
    }

    // ---- measuring ----

    // A text's own size at a width (unbounded: one line), measured by the TextBlock itself, so its font is the one it
    // draws with (a button's words take theirs from the button). Measured again at its last size afterwards, so the
    // layout stays as it was.
    private static Size Measured(TextBlock tb, double width)
    {
        var available = LayoutInformation.GetAvailableSize(tb);
        var m = tb.Margin;
        tb.Measure(new Size(width + m.Left + m.Right, double.PositiveInfinity));
        var desired = tb.DesiredSize;
        tb.Measure(available);
        return new Size(Math.Max(0, desired.Width - m.Left - m.Right), Math.Max(0, desired.Height - m.Top - m.Bottom));
    }

    // A word of a wrapping text that is wider than its line, so it breaks inside the word: measured on a copy of the
    // TextBlock, scaled to what the TextBlock itself measures for the whole text on one line.
    private static string? LongestWord(TextBlock tb, string text, double roomWidth)
    {
        // A path or an address breaks anywhere by nature (diagnostics show them).
        var words = text.Split([' ', '\n', '\u00A0'], StringSplitOptions.RemoveEmptyEntries).Distinct()
            .Where(w => w.Length >= 6 && !w.Contains('\\') && !w.Contains('/'))
            .OrderByDescending(w => w.Length).Take(3).ToList();
        if (words.Count == 0)
            return null;
        var copy = Probe(tb, null);
        var scale = copy.Width > 0 ? Measured(tb, double.PositiveInfinity).Width / copy.Width : 1;
        return words.FirstOrDefault(w => Probe(tb, w).Width * scale > roomWidth + Slack);
    }

    // A copy of a TextBlock outside the window, with its words (or one word) on one line.
    private static Size Probe(TextBlock tb, string? word)
    {
        var probe = new TextBlock
        {
            FontFamily = tb.FontFamily, FontSize = tb.FontSize, FontWeight = tb.FontWeight, FontStyle = tb.FontStyle,
            FontStretch = tb.FontStretch, CharacterSpacing = tb.CharacterSpacing, TextLineBounds = tb.TextLineBounds,
            Padding = tb.Padding, OpticalMarginAlignment = tb.OpticalMarginAlignment, IsTextScaleFactorEnabled = tb.IsTextScaleFactorEnabled,
            TextWrapping = TextWrapping.NoWrap,
        };
        if (word is not null || tb.Inlines.Count == 0)
            probe.Text = word ?? tb.Text;
        else
            Copy(tb.Inlines, probe.Inlines);
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize;
    }

    // The runs with the look they set themselves; the rest they take from the probe, as from their own TextBlock.
    private static void Copy(InlineCollection from, InlineCollection to)
    {
        foreach (var inline in from)
        {
            Inline? copy = inline switch
            {
                Run run => new Run { Text = run.Text },
                LineBreak => new LineBreak(),
                Span span => new Span(),
                _ => null,
            };
            if (copy is null)
                continue;
            foreach (var property in new[] { TextElement.FontFamilyProperty, TextElement.FontSizeProperty, TextElement.FontWeightProperty,
                         TextElement.FontStyleProperty, TextElement.FontStretchProperty, TextElement.CharacterSpacingProperty })
            {
                if (inline.ReadLocalValue(property) != DependencyProperty.UnsetValue)
                    copy.SetValue(property, inline.GetValue(property));
            }
            if (inline is Span from2 && copy is Span to2)
                Copy(from2.Inlines, to2.Inlines);
            to.Add(copy);
        }
    }

    // Where the letters are: as wide as the text, placed as the box aligns it.
    private static Rect InkOf(TextBlock tb, Rect box, Size natural)
    {
        var scaleX = tb.ActualWidth > 0 ? box.Width / tb.ActualWidth : 1;
        var scaleY = tb.ActualHeight > 0 ? box.Height / tb.ActualHeight : 1;
        var width = natural.Width * scaleX;
        var height = natural.Height * scaleY;
        // A TextBlock draws from its top; Right is End and Left is Start.
        var x = tb.TextAlignment switch
        {
            TextAlignment.Center => box.X + (box.Width - width) / 2,
            TextAlignment.Right => box.X + box.Width - width,
            _ => box.X,
        };
        return new Rect(x, box.Y, Math.Max(0, width), Math.Max(0, height));
    }

    // The room an element was given to lay out in: its layout slot without its margins (its own size where it has none).
    private static (double Width, double Height) RoomOf(FrameworkElement fe)
    {
        if (VisualTreeHelper.GetParent(fe) is null)
            return (fe.ActualWidth, fe.ActualHeight);
        var slot = LayoutInformation.GetLayoutSlot(fe);
        var m = fe.Margin;
        return (Math.Max(0, slot.Width - m.Left - m.Right), Math.Max(0, slot.Height - m.Top - m.Bottom));
    }

    // ---- geometry ----

    private Rect? BoundsOf(UIElement element)
    {
        if (element is not FrameworkElement fe)
            return null;
        return Transform(fe, new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
    }

    // From an element's own coordinates to the window's, in DIP.
    private static Rect? Transform(UIElement element, Rect rect)
    {
        try
        {
            return element.TransformToVisual(null).TransformBounds(rect);
        }
        catch (Exception e) when (e is ArgumentException or COMException)
        {
            return null;
        }
    }

    private int[] Pixels(Rect r) =>
        [(int)Math.Round(r.X * _scale), (int)Math.Round(r.Y * _scale), (int)Math.Round(r.Width * _scale), (int)Math.Round(r.Height * _scale)];

    private static bool Contains(Rect room, Rect r) =>
        r.Left >= room.Left - Slack && r.Top >= room.Top - Slack && r.Right <= room.Right + Slack && r.Bottom <= room.Bottom + Slack;

    private static Rect Intersect(Rect a, Rect b)
    {
        var x = Math.Max(a.Left, b.Left);
        var y = Math.Max(a.Top, b.Top);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);
        return right > x && bottom > y ? new Rect(x, y, right - x, bottom - y) : new Rect(x, y, 0, 0);
    }

    private static Rect Union(Rect a, Rect b)
    {
        var x = Math.Min(a.Left, b.Left);
        var y = Math.Min(a.Top, b.Top);
        return new Rect(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }

    private static Rect Deflate(Rect r) =>
        new(r.X + Slack, r.Y + Slack, Math.Max(0, r.Width - 2 * Slack), Math.Max(0, r.Height - 2 * Slack));

    // ---- naming an element ----

    /// <summary>Its x:Name, else its type in the nearest named element around it, with the steps between.</summary>
    private string Describe(DependencyObject element, IReadOnlyList<DependencyObject> chain)
    {
        var type = element.GetType().Name;
        if (element is FrameworkElement { Name: { Length: > 0 } name })
            return $"{name} ({type})";
        var steps = new List<string>();
        var at = chain.Count - 1;
        while (at >= 0 && !ReferenceEquals(chain[at], element))
            at--;
        // Within the picture, names from the window's own XAML; above it (a popup's frame), any.
        var top = at >= _rootDepth ? _rootDepth : 0;
        for (var i = at - 1; i >= top; i--)
        {
            if (chain[i] is FrameworkElement { Name: { Length: > 0 } named })
            {
                steps.Insert(0, named);
                break;
            }
            var step = chain[i].GetType().Name;
            if (!step.EndsWith("Presenter", StringComparison.Ordinal) && step != "ContentControl")
                steps.Insert(0, step);
        }
        if (steps.Count > 4)
            steps = [steps[0], "…", .. steps[^3..]];
        return steps.Count == 0 ? type : $"{type} in {string.Join(" › ", steps)}";
    }

    // ---- the files ----

    private string Json(string picture)
    {
        var ordered = _problems.OrderBy(p => Array.IndexOf(Kinds, p.Kind)).ToList();
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].Number = i + 1;
        _problems.Clear();
        _problems.AddRange(ordered);
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteString("picture", picture);
            json.WriteString("language", _context.Language);
            json.WriteString("culture", _context.Culture);
            json.WriteBoolean("pseudo", _context.Pseudo);
            json.WriteStartObject("window");
            json.WriteNumber("width", Math.Round(_window.Width));
            json.WriteNumber("height", Math.Round(_window.Height));
            json.WriteNumber("scale", Math.Round(_scale, 3));
            json.WriteNumber("outerWidth", _context.OuterWindow.Width);
            json.WriteNumber("outerHeight", _context.OuterWindow.Height);
            json.WriteEndObject();
            json.WriteNumber("texts", _shown.Count(s => s.InView));
            json.WriteNumber("textsNotInView", _shown.Count(s => !s.InView));
            json.WriteStartObject("counts");
            foreach (var kind in Kinds)
                json.WriteNumber(kind, _problems.Count(p => p.Kind == kind));
            json.WriteEndObject();
            json.WriteStartArray("problems");
            foreach (var p in _problems)
            {
                json.WriteStartObject();
                json.WriteNumber("n", p.Number);
                json.WriteString("kind", p.Kind);
                json.WriteString("element", p.Element);
                json.WriteString("text", Masked(p.Text));
                WriteBounds(json, "bounds", p.Bounds);
                json.WriteString("detail", Masked(p.Detail));
                json.WriteBoolean("inView", p.InView);
                if (p.Other is not null)
                {
                    json.WriteString("otherElement", p.OtherElement);
                    json.WriteString("otherText", Masked(p.OtherText ?? ""));
                    WriteBounds(json, "otherBounds", p.Other);
                }
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\n", "\r\n") + "\r\n";
    }

    private static void WriteBounds(Utf8JsonWriter json, string name, int[] bounds)
    {
        json.WriteStartArray(name);
        foreach (var v in bounds)
            json.WriteNumberValue(v);
        json.WriteEndArray();
    }

    private static readonly Dictionary<string, SKColor> KindColours = new()
    {
        ["overflow"] = new SKColor(0xFF, 0x30, 0x30),
        ["trimmed"] = new SKColor(0xFF, 0x9F, 0x1A),
        ["overlap"] = new SKColor(0xFF, 0x40, 0xFF),
        ["untranslated"] = new SKColor(0x20, 0xD0, 0xFF),
    };

    // The picture with each problem's box and number, in its kind's colour.
    private static void DrawProblems(byte[] pixels, int width, int height, List<Problem> problems, List<List<SKRect>> boxes, double toPicture, string path)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), Math.Min(pixels.Length, bitmap.ByteCount));
        using (var canvas = new SKCanvas(bitmap))
        {
            var unit = (float)Math.Max(1, toPicture);
            using var typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold) ?? SKTypeface.Default;
            using var font = new SKFont(typeface, 11 * unit);
            var picture = new SKRect(0, 0, width, height);
            for (var i = 0; i < problems.Count; i++)
            {
                var colour = KindColours.GetValueOrDefault(problems[i].Kind, SKColors.Red);
                var thin = problems[i].Kind == "untranslated";
                using var stroke = new SKPaint { Color = colour, Style = SKPaintStyle.Stroke, StrokeWidth = (thin ? 1 : 2) * unit, IsAntialias = true };
                using var fill = new SKPaint { Color = colour, Style = SKPaintStyle.Fill };
                using var ink = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                var label = problems[i].Number.ToString(CultureInfo.InvariantCulture);
                var labelled = false;
                foreach (var box in boxes[i])
                {
                    var shown = SKRect.Intersect(box, picture);
                    var text = label;
                    if (shown.IsEmpty)
                    {
                        // Outside the picture (scrolled out, below the window's edge): its number at the edge it lies
                        // beyond, with an arrow.
                        var arrow = box.Top >= height ? "↓" : box.Bottom <= 0 ? "↑" : box.Left >= width ? "→" : "←";
                        var x = Math.Clamp(box.Left, 0, width - 1);
                        var y0 = Math.Clamp(box.Top, 0, height - 1);
                        shown = new SKRect(x, y0, x + 1, y0 + 1);
                        text = label + arrow;
                    }
                    else
                    {
                        canvas.DrawRect(box, stroke);
                    }
                    if (labelled)
                        continue;
                    labelled = true;
                    var w = font.MeasureText(text) + 6 * unit;
                    var h = 14 * unit;
                    var y = shown.Top - h >= 0 ? shown.Top - h : shown.Top;
                    var left = Math.Min(shown.Left, width - w);
                    var tag = new SKRect(left, y, left + w, y + h);
                    label = text;
                    canvas.DrawRect(tag, fill);
                    canvas.DrawText(label, tag.Left + 3 * unit, tag.Bottom - 3 * unit, SKTextAlign.Left, font, ink);
                }
            }
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }
}
#endif
