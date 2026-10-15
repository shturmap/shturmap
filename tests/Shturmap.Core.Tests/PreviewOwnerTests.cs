using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// Who decides what the map shows while the pointer asks for previews (owner, 2026-10-15, on What's New's tour:5 line:
// "the highlight does weird movements and highlights the wrong things sometimes"): the pointer, once its wait is over,
// except while the tour is up, which stages its chapters on the map and ends them itself.
public class PreviewOwnerTests
{
    [Theory]
    // Outside the tour: the pointer's last request wins.
    [InlineData(false, null, null, PreviewOwner.Step.Stay)]
    [InlineData(false, null, "woods", PreviewOwner.Step.Start)]
    [InlineData(false, "woods", "woods", PreviewOwner.Step.Stay)]
    [InlineData(false, "woods", "customs", PreviewOwner.Step.Start)]
    [InlineData(false, "woods", null, PreviewOwner.Step.End)]
    [InlineData(false, "whatsnew:0.5.0:0", null, PreviewOwner.Step.End)]
    [InlineData(false, "whatsnew:0.5.0:0", "whatsnew:0.5.0:2", PreviewOwner.Step.Start)]
    // While the tour is up, nothing the pointer asked for starts or ends anything. The report: the tour:5 line's leaving,
    // under the tour's layer, asked for the end while YOUR SCREENSHOT KEY was staged.
    [InlineData(true, "tour:12", null, PreviewOwner.Step.Stay)]
    [InlineData(true, "tour:12", "woods", PreviewOwner.Step.Stay)]
    [InlineData(true, null, "whatsnew:0.5.0:0", PreviewOwner.Step.Stay)]
    [InlineData(true, "woods", null, PreviewOwner.Step.Stay)]
    [InlineData(true, null, null, PreviewOwner.Step.Stay)]
    public void The_wait_over_starts_or_ends_a_preview_only_while_the_tour_is_down(bool tourOpen, string? previewing, string? wanted, PreviewOwner.Step step) =>
        Assert.Equal(step, PreviewOwner.After(tourOpen, previewing, wanted));

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void The_pointers_requests_count_only_while_the_tour_is_down(bool tourOpen, bool heeded) =>
        Assert.Equal(heeded, PreviewOwner.Heeds(tourOpen));
}
