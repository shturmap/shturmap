using Shturmap.Core;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// The highlighted quest's places out of view are shown as chevrons at the edge (cartography review, 2026-10-02).
public class ChevronTests
{
    [Fact]
    public void Places_out_of_view_point_from_the_edge_with_a_count()
    {
        // The view spans x and z from -500 to 500.
        var (camera, scene) = Of([Quest("a", 0, 0, "Revision", group: "revision"), Quest("b", 900, 10, "Revision", group: "revision"),
            Quest("c", 950, -10, "Revision", group: "revision"), Quest("d", 0, -900, "Revision", group: "revision"), Quest("e", 900, 0, "Dandies")]);
        scene.Selected = "revision";
        var chevrons = MapRenderer.Layout(camera, scene, 1).Chevrons;
        Assert.Equal(2, chevrons.Count);
        var right = chevrons.Single(c => c.At.X > 900);
        Assert.Equal(2, right.Count);
        Assert.InRange(right.Degrees, -5, 5);
        var below = chevrons.Single(c => c.At.Y > 900);
        Assert.Equal(1, below.Count);
        Assert.InRange(below.Degrees, 85, 95);
        Assert.All(chevrons, c => Assert.Equal(MapRenderer.Kept, c.Color));
    }

    [Fact]
    public void Nothing_highlighted_means_no_chevrons()
    {
        var (camera, scene) = Of([Quest("b", 900, 0, "Revision", group: "revision")]);
        Assert.Empty(MapRenderer.Layout(camera, scene, 1).Chevrons);
    }
}
