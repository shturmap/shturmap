namespace Shturmap.App.Rules;

/// <summary>
/// Who decides what the map shows while the pointer asks for previews: resting on a map's row in Plan or on a What's
/// New line asks for its preview, leaving asks for its end, each heeded once its wait is over (MainWindow.Preview).
/// While the tour is up, the tour does: it stages each chapter's example on the map and ends it itself
/// (MainWindow.Tour, EndTourStage), and the pointer's requests are not heeded (owner, 2026-10-15, on What's New's
/// "Your extracts, clearer", tour:5: "the highlight does weird movements and highlights the wrong things sometimes").
/// A click on that line opened the tour with the pointer on the line; the tour's layer covered it, so the pointer's
/// next move left the line, and its leaving asked for the end of a preview. 0.3 s later that end came: the player's
/// own map was put back under YOUR SCREENSHOT KEY while the chapter went on staging its example on the scene that had
/// gone, moving the view and placing its cut-outs by a map no longer shown.
/// </summary>
public static class PreviewOwner
{
    /// <summary>What the preview's wait does when it is over.</summary>
    public enum Step
    {
        /// <summary>Nothing: what is shown is what was asked for, or the tour has the map.</summary>
        Stay,

        /// <summary>The preview asked for shows.</summary>
        Start,

        /// <summary>The preview showing ends: the map comes back as it was.</summary>
        End,
    }

    /// <summary>Whether a request of the pointer's counts now: not while the tour is up. One that came before it opened
    /// is dropped as it opens (MainWindow.OpenTour).</summary>
    public static bool Heeds(bool tourOpen) => !tourOpen;

    /// <summary>
    /// The wait is over: the preview asked for last (<paramref name="wanted"/>, null for none) against the one showing
    /// (<paramref name="previewing"/>, null for none; the tour's own staging is one too).
    /// </summary>
    public static Step After(bool tourOpen, string? previewing, string? wanted) =>
        tourOpen || wanted == previewing ? Step.Stay : wanted is null ? Step.End : Step.Start;
}
