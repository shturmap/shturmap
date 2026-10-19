using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Rules;
using Shturmap.Session;

namespace Shturmap.App.Controls;

/// <summary>
/// How large the window draws its text (docs/DESIGN.md §4, "Screen anatomy", *Text size*; owner, 2026-10-19: Windows'
/// text size honoured everywhere, and "set by an option, not only through the windows settings"). WinUI draws every
/// text at Windows' text size (Settings › Accessibility › Text size) by itself; settings' step (Rules.TextSize) adds its
/// share here: every text in the window and its open popups (a TextBlock's, its runs', a RichTextBlock's, a text box's,
/// a FontIcon's glyph) takes the size WinUI's own curve gives at the step, so the step grows text as Windows' setting
/// does, smaller text more. The map, which WinUI doesn't reach, takes the whole factor (MapView.UiScale), and so does
/// the smallest window (MainWindow.ApplySmallestSize). A change, in settings or in Windows', applies at once.
/// The pass runs after a layout that may have brought new text, at most every <see cref="Every"/> ms, and costs nothing
/// while the step adds nothing (Normal, and no other step this run). In a developer build "--text-scale" stands in for
/// the whole factor, so snapshots show Windows' text sizes without touching Windows' setting (§8, "Developer aids").
/// </summary>
internal static class TextScale
{
    /// <summary>Windows' text size (UISettings.TextScaleFactor, 1 to 2.25), followed while the app runs.</summary>
    public static double WindowsFactor { get; private set; } = 1;

    /// <summary>Settings' step.</summary>
    public static TextStep Step { get; private set; }

    /// <summary>A developer build's "--text-scale": the whole factor, in place of Windows' and the step's.</summary>
    public static double? Simulated { get; private set; }

    /// <summary>The whole factor the text is drawn at: the map's labels and symbols and the smallest window follow it.</summary>
    public static double Effective => Simulated ?? TextSize.Effective(WindowsFactor, Step);

    /// <summary>The share this pass applies to the window's text (WinUI applies Windows' own).</summary>
    public static double Own => Simulated ?? TextSize.Own(WindowsFactor, Step);

    /// <summary>The size a text written at <paramref name="size"/> is drawn at: the step's share by this pass, then
    /// Windows' by WinUI, both by WinUI's curve.</summary>
    public static double Drawn(double size) =>
        Simulated is { } f ? TextSize.Scaled(size, f) : TextSize.Scaled(TextSize.Scaled(size, Own), WindowsFactor);

    /// <summary>The factor changed (settings' step or Windows' text size), on the window's thread, after the window's
    /// text took it: what is measured off text, and the map, follow.</summary>
    public static event Action? Changed;

    // Windows' settings, kept: its change event stops with the object.
    private static Windows.UI.ViewManagement.UISettings? _windows;
    private static DispatcherQueue? _queue;
    private static Window? _window;
    private static DispatcherQueueTimer? _later;
    private static long _lastPass;
    private static bool _touched;

    // A pass runs at most this often; a layout in between is looked at when it is up.
    private const int Every = 100;

    // Anything to do: the step adds something, or put sizes back after it did.
    private static bool Active => Own != 1 || _touched;

    /// <summary>The step read from settings before the window shows, and a developer run's factor.</summary>
    public static void Prepare(TextStep step, double? simulated)
    {
        Step = step;
        Simulated = simulated is { } f ? Math.Clamp(f, 1, TextSize.Most) : null;
        try
        {
            _windows = new Windows.UI.ViewManagement.UISettings();
            WindowsFactor = _windows.TextScaleFactor;
        }
        catch (Exception e)
        {
            AppLog.Warn("Reading Windows' text size failed", e);
        }
        AppLog.Info(Said("Text size"));
    }

    /// <summary>Follows Windows' setting and every layout of the window from now on.</summary>
    public static void Start(Window window)
    {
        _window = window;
        _queue = window.DispatcherQueue;
        if (_windows is not null)
            _windows.TextScaleFactorChanged += OnWindowsChanged;
        var root = window.Content as FrameworkElement;
        if (root is not null)
            root.LayoutUpdated += OnLayoutUpdated;
        window.Closed += (_, _) =>
        {
            if (_windows is not null)
                _windows.TextScaleFactorChanged -= OnWindowsChanged;
            if (root is not null)
                root.LayoutUpdated -= OnLayoutUpdated;
            _later?.Stop();
        };
        if (Active)
            Pass();
    }

    /// <summary>Settings' step changed: the window's text takes it at once.</summary>
    public static void SetStep(TextStep step)
    {
        if (step == Step)
            return;
        Step = step;
        Apply();
    }

    /// <summary>Scales a text measured off the window (a fit's probe, a card before it is shown) as the window's are.</summary>
    public static void Probe(FrameworkElement probe)
    {
        if (Active)
            Scale(probe);
    }

    /// <summary>Scales the window's text and its open popups now (before a picture of a popup just opened).</summary>
    public static void Now()
    {
        if (Active)
            Pass();
    }

    /// <summary>What the factor comes to, for the app log.</summary>
    public static string Said(string what)
    {
        var simulated = Simulated is { } f ? string.Create(CultureInfo.InvariantCulture, $", --text-scale {f:0.###}") : "";
        return string.Create(CultureInfo.InvariantCulture,
            $"{what}: ×{Effective:0.###} (Windows {WindowsFactor:0.###}, settings {TextSize.Format(Step)}{simulated})");
    }

    // Windows' text size changed (raised off the window's thread).
    private static void OnWindowsChanged(Windows.UI.ViewManagement.UISettings sender, object args) =>
        _queue?.TryEnqueue(() =>
        {
            var now = sender.TextScaleFactor;
            if (now == WindowsFactor)
                return;
            WindowsFactor = now;
            Apply();
        });

    private static void Apply()
    {
        AppLog.Info(Said("Text size"));
        _logPass = true;
        if (Active)
            Pass();
        Changed?.Invoke();
    }

    // After a layout: new text may have come (a row, a card, an open flyout), or code set a size anew.
    private static void OnLayoutUpdated(object? sender, object e)
    {
        if (!Active || _queue is null)
            return;
        var since = Environment.TickCount64 - _lastPass;
        if (since >= Every)
        {
            Pass();
            return;
        }
        if (_later is null)
        {
            _later = _queue.CreateTimer();
            _later.IsRepeating = false;
            _later.Tick += (_, _) => Pass();
        }
        if (!_later.IsRunning)
        {
            _later.Interval = TimeSpan.FromMilliseconds(Every - since);
            _later.Start();
        }
    }

    // What the passes cost, said in the app log: the first at a size, each slower than any before, and once a minute
    // how many ran and how long they took in all.
    private static bool _logPass = true;
    private static double _slowest;
    private static int _passes;
    private static double _spent;
    private static long _minute;
    private static int _visited;
    private static int _sized;

    private static void Pass()
    {
        _lastPass = Environment.TickCount64;
        if (_window?.Content is not UIElement content)
            return;
        var watch = Stopwatch.StartNew();
        _visited = _sized = 0;
        Scale(content);
        if (content.XamlRoot is { } root)
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root))
                if (popup.Child is { } child)
                    Scale(child);
        var took = watch.Elapsed.TotalMilliseconds;
        if (_logPass || took > _slowest * 1.5)
        {
            _logPass = false;
            _slowest = Math.Max(_slowest, took);
            AppLog.Info(string.Create(CultureInfo.InvariantCulture,
                $"Text size ×{Own:0.###} on the window's text: {_visited} elements looked at, {_sized} sized, in {took:0.#} ms"));
        }
        _passes++;
        _spent += took;
        if (_minute == 0)
            _minute = _lastPass;
        else if (_lastPass - _minute >= 60_000)
        {
            AppLog.Info(string.Create(CultureInfo.InvariantCulture,
                $"Text size: {_passes} passes over the window in the last {(_lastPass - _minute) / 1000} s, {_spent:0} ms in all"));
            _minute = _lastPass;
            _passes = 0;
            _spent = 0;
        }
    }

    private static void Scale(DependencyObject element)
    {
        _visited++;
        switch (element)
        {
            // Hidden: looked at when it shows, which lays the window out again.
            case UIElement { Visibility: Visibility.Collapsed }:
                return;
            case TextBlock text:
                Size(text, TextBlock.FontSizeProperty, text.FontSize);
                foreach (var inline in text.Inlines)
                    Scale(inline);
                return;
            case RichTextBlock rich:
                Size(rich, RichTextBlock.FontSizeProperty, rich.FontSize);
                foreach (var block in rich.Blocks)
                    if (block is Paragraph paragraph)
                        foreach (var inline in paragraph.Inlines)
                            Scale(inline);
                return;
            case FontIcon icon:
                Size(icon, FontIcon.FontSizeProperty, icon.FontSize);
                return;
            // A text box draws its own text; its placeholder takes the box's size.
            case TextBox box:
                Size(box, Control.FontSizeProperty, box.FontSize);
                return;
        }
        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var i = 0; i < count; i++)
            Scale(VisualTreeHelper.GetChild(element, i));
        // Not yet in the visual tree (a card measured before it is shown): its content as written.
        if (count == 0)
        {
            var child = element switch
            {
                UserControl user => user.Content,
                Border border => border.Child,
                ContentControl control => control.Content as UIElement,
                _ => null,
            };
            if (child is not null)
                Scale(child);
            else if (element is Panel panel)
                foreach (var each in panel.Children)
                    Scale(each);
        }
    }

    // A run's own size, where it has one; otherwise it takes its text block's.
    private static void Scale(Inline inline)
    {
        if (inline.ReadLocalValue(TextElement.FontSizeProperty) != DependencyProperty.UnsetValue)
            Size(inline, TextElement.FontSizeProperty, inline.FontSize);
        if (inline is Span span)
            foreach (var each in span.Inlines)
                Scale(each);
    }

    // Every size the pass gave at any share (as WinUI keeps it, a float) with the size it was written at, and the sizes
    // that are done at each share. A text at a size given at the share now is done, whoever set it (this pass, or
    // code copying another text's size: the roll call's halo, a fit's probe); one at a size given at another share goes
    // back to its size as written and from there to the share now; any other size is as written, set anew by code or a
    // binding, or never seen. Kept by size, not by element: a managed wrapper of an element nothing in C# holds (an icon
    // in a template) is collected and made anew, so a table by element forgot it and scaled it again and again (the
    // developer aid's first runs, 2026-10-19), and reading marks kept on each element cost more than the sizing.
    private static readonly Dictionary<float, double> Written = [];
    private static readonly Dictionary<double, HashSet<float>> GivenAt = [];

    private static void Size(DependencyObject element, DependencyProperty property, double size)
    {
        var now = (float)size;
        var share = Own;
        if (!GivenAt.TryGetValue(share, out var given))
            GivenAt[share] = given = [];
        if (given.Contains(now))
            return;
        var written = Written.TryGetValue(now, out var was) ? was : size;
        var scaled = TextSize.Scaled(written, share);
        if ((float)scaled != now)
        {
            element.SetValue(property, scaled);
            _sized++;
        }
        var set = (float)(double)element.GetValue(property);
        if (set != (float)written)
            Written[set] = written;
        given.Add(set);
        _touched |= share != 1;
    }
}
