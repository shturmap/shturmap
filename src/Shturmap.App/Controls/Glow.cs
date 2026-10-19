using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace Shturmap.App.Controls;

/// <summary>
/// A mark that just arrived glows in (owner, 2026-10-11: "a little eyecandy animation when the app recognises the
/// player updates the stash … give proper feedback"; docs/DESIGN.md §4, "What you have"): a gold copy of the mark's
/// line, or a gold bar under a need cell, lies over the ink one and fades out over a second, so the gold eases to
/// ink; and the row flashes once, faintly. With Windows' animation effects off nothing of it shows: the mark stands,
/// in ink. The overlays are in the templates (MainWindow.xaml's RequirementTemplate, QuestCard.xaml's BRING rows),
/// shown only for a fresh mark, and start fading as they load.
/// </summary>
internal static class Glow
{
    /// <summary>How long the gold takes to ease to ink.</summary>
    public static readonly TimeSpan Length = TimeSpan.FromSeconds(1.2);

    /// <summary>
    /// The name of a line's gold copy over its words (x:Name in the templates): the layout check takes it for a layer
    /// that lies over them on purpose, not for text over text (Dev.LayoutCheck.Layers; QA, 2026-10-19).
    /// </summary>
    public const string CopyName = "GlowCopy";

    /// <summary>The row's flash: this much gold over it, gone in under a second.</summary>
    public const double FlashOpacity = 0.22;
    public static readonly TimeSpan FlashLength = TimeSpan.FromSeconds(0.9);

    private static readonly Windows.UI.ViewManagement.UISettings Settings = new();

    /// <summary>An overlay just loaded: from <paramref name="from"/> to nothing over <paramref name="length"/>; with
    /// animation effects off, nothing at all.</summary>
    public static void Fade(UIElement overlay, double from, TimeSpan length)
    {
        if (!Settings.AnimationsEnabled)
        {
            overlay.Opacity = 0;
            return;
        }
        overlay.Opacity = from;
        var fade = new DoubleAnimation { From = from, To = 0, Duration = length, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        Storyboard.SetTarget(fade, overlay);
        Storyboard.SetTargetProperty(fade, "Opacity");
        new Storyboard { Children = { fade } }.Begin();
    }

    /// <summary>The Loaded handler of an overlay in a template: a line's gold copy, or, tagged "flash", the row's
    /// flash. One that isn't shown (the mark isn't fresh) is let be.</summary>
    public static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Visibility: Visibility.Visible } overlay)
            return;
        var flash = overlay.Tag is "flash";
        Fade(overlay, flash ? FlashOpacity : 1, flash ? FlashLength : Length);
    }
}
