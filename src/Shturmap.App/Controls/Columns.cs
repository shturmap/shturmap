using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Size = Windows.Foundation.Size;

namespace Shturmap.App.Controls;

/// <summary>
/// Columns of labels that line up what stands beside them, as wide as their longest label in the language in use, and
/// never narrower than they were in English: a fixed width that fit English cut a longer language's word off or ran it
/// into its neighbour (review of 2026-10-10). Read again when the language changes (Bindings.Update).
/// </summary>
public static class Columns
{
    /// <summary>
    /// The raid card's glance labels (NEXT, EXIT, OR), so the names beside them line up within the card; the rail's card
    /// and the tour's example card both take it (German "EXTRACT" filled the fixed 48 DIP and ran into the extract's
    /// name). The label's longest word and the gap after it, as between a row's parts ("Design system", spacing).
    /// </summary>
    public static GridLength Glance => new(Widest("EyebrowText", [ViewTexts.RaidNextLabel, ViewTexts.RaidExitLabel, ViewTexts.RaidOrLabel], 10, 48));

    /// <summary>Help's keys, each a keycap (6 DIP either side of its word, a 1 DIP frame), before what it does.</summary>
    public static GridLength HelpKeys => new(Widest("StatusText",
        [AppTexts.HelpKeyShowMe, AppTexts.HelpKeyFollow, AppTexts.HelpKeyZoom, AppTexts.HelpKeyFit, AppTexts.HelpKeyFloor, AppTexts.HelpKeyClose,
         AppTexts.HelpKeyHelp, AppTexts.HelpKeySettings, AppTexts.HelpKeyMouse], 14, 96));

    /// <summary>
    /// Help's quest types, each name (semibold, in help's own size) before what it means: as wide as the longest name,
    /// never narrower than the 96 DIP English had (2026-10-19: at 150 % text the fixed 96 cut "Elimination",
    /// "Exploration" and "Find in raid").
    /// </summary>
    public static GridLength HelpKinds => new(Widest(() => new TextBlock
    {
        FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["UiFont"],
        FontSize = 14,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
    }, Enum.GetValues<Shturmap.Core.Quests.ObjectiveKind>().Select(Shturmap.Core.Quests.QuestTaxonomy.Label), 0, 96));

    // The widest of these texts in a text style, measured as drawn (outside the window, its own margin aside), and what
    // goes beside it; at least the least.
    private static double Widest(string style, IEnumerable<string> texts, double beside, double least)
    {
        var look = (Style)Application.Current.Resources[style];
        return Widest(() => new TextBlock { Style = look }, texts, beside, least);
    }

    private static double Widest(Func<TextBlock> make, IEnumerable<string> texts, double beside, double least)
    {
        var widest = 0.0;
        foreach (var text in texts)
        {
            var probe = make();
            probe.Text = text;
            probe.Margin = new Thickness(0);
            TextScale.Probe(probe);
            probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, probe.DesiredSize.Width);
        }
        return Math.Max(least, Math.Ceiling(widest + beside));
    }
}
