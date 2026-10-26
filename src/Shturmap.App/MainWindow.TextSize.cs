using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Controls;
using Shturmap.App.Rules;
using Shturmap.Core.Navigation;
using Shturmap.Session;

namespace Shturmap.App;

// The text size (owner, 2026-10-19: Windows' text size honoured everywhere, and "set by an option, not only through the
// windows settings"; docs/DESIGN.md §4, "Screen anatomy", *Text size*): settings' row, and what follows a change of it or
// of Windows' text size while Shturmap runs. The window's text takes it in Controls.TextScale; here the columns measured
// off text are measured again, the fits looked at again, the map drawn again and the smallest window set again.
public sealed partial class MainWindow
{
    private bool _glanceStacked;

    private void WatchTextSize()
    {
        ViewModel.TextSizeStep = TextScale.Step.ToString();
        TextScale.Changed += OnTextSizeChanged;
        Closed += (_, _) => TextScale.Changed -= OnTextSizeChanged;
    }

    public Brush ChosenBrush(string chosen, string value) => Resource(chosen == value ? "AmberBrush" : "MutedBrush");

    private void OnTextSizeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } || !Enum.TryParse<TextStep>(tag, out var step) || step == TextScale.Step)
            return;
        Study.Ui("settings.textSize", ("step", TextSize.Format(step)));
        ChooseTextSize(step);
    }

    // A step chosen, by the row or a developer script's "textsize": saved, shown as chosen, and taken at once.
    private void ChooseTextSize(TextStep step)
    {
        if (step == TextScale.Step)
            return;
        _session.SetSetting(TextSize.Setting, TextSize.Format(step));
        ViewModel.TextSizeStep = step.ToString();
        TextScale.SetStep(step);
    }

    private void OnTextSizeChanged()
    {
        // The columns measured off text (the glance's labels, help's keys and quest types) and every x:Bind with them.
        Bindings.Update();
        ViewModel.TextSizeStep = TextScale.Step.ToString();
        _twoDigits = null;
        if (Volatile.Read(ref _snapshot) is { } s)
            UpdateTraders(s);
        FitStatusBar();
        FitGlance();
        Map.Redraw();
        PlaceInsideGrid();
        ApplySmallestSize();
    }

    private void OnGlanceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.PreviousSize.Width != e.NewSize.Width)
            FitGlance();
    }

    // The glance's labels beside their names, or above them where the names would be left too little room
    // (Rules.GlanceFold): all three rows alike, so the names still line up. Looked at again when the row's width, the
    // language or the text size changes.
    private void FitGlance()
    {
        Grid[] rows = [GlanceNext, GlanceExit, GlanceOr];
        if (rows.FirstOrDefault(r => r.ActualWidth > 0) is not { } row)
            return;
        var inner = row.ActualWidth - row.Padding.Left - row.Padding.Right;
        var stacked = GlanceFold.Stacked(inner, Columns.Glance.Value, GlanceFigures(), TextScale.Drawn(GlanceNameSize));
        if (stacked == _glanceStacked && !stacked)
            return;
        _glanceStacked = stacked;
        foreach (var each in rows)
            StackGlance(each, stacked);
    }

    // As written in MainWindow.xaml: the names' size, and the figures' column (its gap before it, and the widest of a
    // distance and a direction).
    private const double GlanceNameSize = 15;
    private const double GlanceFigureGap = 10;

    private double GlanceFigures()
    {
        var widest = Measured("FigureText", 22, "888 m").Width;
        foreach (var direction in Enum.GetValues<RelativeDirection>())
            widest = Math.Max(widest, Measured("StatusText", 11.5, Caps.Of(Bearing.Describe(direction))).Width);
        return widest + GlanceFigureGap;
    }

    // A text as the window draws it, measured off the window.
    private static Windows.Foundation.Size Measured(string style, double size, string text)
    {
        var probe = new TextBlock { Style = (Style)Application.Current.Resources[style], FontSize = size, Text = text, Margin = new Thickness(0) };
        TextScale.Probe(probe);
        probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize;
    }

    // A glance row: its label (column 0), its name and what goes with it (column 1), its figures (column 2), in one row.
    // Stacked, the label spans the first two columns and the name stands under it across them: it starts below the
    // label (its top, its line, a gap of 3), the figures still at the right beside both.
    private static void StackGlance(Grid row, bool stacked)
    {
        var label = row.Children.OfType<TextBlock>().First(t => Grid.GetColumn(t) == 0);
        var name = row.Children.OfType<StackPanel>().First(p => Grid.GetColumn(p) != 2);
        Grid.SetColumnSpan(label, stacked ? 2 : 1);
        Grid.SetColumn(name, stacked ? 0 : 1);
        Grid.SetColumnSpan(name, stacked ? 2 : 1);
        var top = stacked ? label.Margin.Top + Measured("EyebrowText", label.FontSize, label.Text).Height + 3 : name.Padding.Bottom;
        if (name.Padding.Top != top)
            name.Padding = new Thickness(0, top, 0, name.Padding.Bottom);
    }
}
