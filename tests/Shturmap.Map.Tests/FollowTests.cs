using Shturmap.Core;
using Shturmap.Core.Maps;
using SkiaSharp;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Follow my position (owner, 2026-10-03): a toggle in the map view; on, the view glides to each new position.
public class FollowTests
{
    private static readonly MapPoint From = new(-40, 25);
    private static readonly MapPoint To = new(160, -75);

    [Fact]
    public void Following_is_off_at_first() => Assert.False(new FollowState().On);

    // Owner, 2026-10-04: "when you have the follow on you should be able to drag and zoom. When a position is updated,
    // it should center back to the player but at the current zoom level." Only the toggle turns following off.
    [Fact]
    public void Dragging_the_map_takes_the_view_for_now_and_following_stays_on()
    {
        var follow = new FollowState();
        follow.Set(true);
        follow.Dragged();
        Assert.True(follow.On);
        Assert.True(follow.Away);
        // The next position brings the view back, and it is on the player again.
        follow.Centred();
        Assert.True(follow.On);
        Assert.False(follow.Away);
    }

    [Fact]
    public void Showing_the_whole_map_keeps_following_too()
    {
        var follow = new FollowState();
        follow.Set(true);
        follow.Fitted();
        Assert.True(follow.On);
        Assert.True(follow.Away);
    }

    [Fact]
    public void Not_following_a_drag_changes_nothing()
    {
        var follow = new FollowState();
        follow.Dragged();
        follow.Fitted();
        Assert.False(follow.On);
        Assert.False(follow.Away);
    }

    [Fact]
    public void Zooming_keeps_the_player_in_the_middle_until_the_view_was_dragged_away()
    {
        var follow = new FollowState();
        Assert.False(follow.Zoomed());
        follow.Set(true);
        Assert.True(follow.Zoomed());
        // Dragged away, the zoom is free: it must not pull the view back to the player.
        follow.Dragged();
        Assert.False(follow.Zoomed());
        // Back on the player (a new position, or F), zooming is about the player again.
        follow.Centred();
        Assert.True(follow.Zoomed());
        Assert.True(follow.On);
    }

    [Fact]
    public void Turning_following_on_starts_on_the_player()
    {
        var follow = new FollowState();
        follow.Set(true);
        follow.Dragged();
        follow.Set(false);
        follow.Set(true);
        Assert.False(follow.Away);
    }

    [Fact]
    public void The_glide_starts_where_the_view_is_and_ends_on_the_target()
    {
        Assert.Equal(From, Camera.PanAt(From, To, 0));
        Assert.Equal(To, Camera.PanAt(From, To, 1));
        // A late frame never overshoots, an early one never goes back.
        Assert.Equal(To, Camera.PanAt(From, To, 1.4));
        Assert.Equal(From, Camera.PanAt(From, To, -0.2));
    }

    [Fact]
    public void The_glide_goes_straight_and_eases_out()
    {
        var previous = 0.0;
        var steps = new List<double>();
        for (var i = 1; i <= 20; i++)
        {
            var p = Camera.PanAt(From, To, i / 20.0);
            var done = (p.X - From.X) / (To.X - From.X);
            // On the line from start to target, always forward.
            Assert.Equal(done, (p.Y - From.Y) / (To.Y - From.Y), 9);
            Assert.True(done > previous, $"step {i}: {done} after {previous}");
            steps.Add(done - previous);
            previous = done;
        }
        // Ease-out: quick at first, slowing into the target.
        for (var i = 1; i < steps.Count; i++)
            Assert.True(steps[i] < steps[i - 1], $"step {i + 1} ({steps[i]}) not slower than step {i} ({steps[i - 1]})");
        // Half the time gone, most of the way there.
        Assert.True(Camera.PanAt(From, To, 0.5).X - From.X > 0.8 * (To.X - From.X));
    }

    [Fact]
    public void The_target_is_the_player_and_the_glide_ends_with_it_in_the_middle_at_the_same_zoom()
    {
        var (camera, scene) = Of([]);
        Assert.Null(FollowState.Target(scene));
        scene.Player = new PlayerFix(new WorldPoint(120, 0, -80), null, DateTime.Now);
        var target = FollowState.Target(scene)!.Value;
        Assert.Equal(scene.Projection.ToMap(scene.Player.Position), target);

        camera.Restore(new MapPoint(0, 0), 2.5);
        camera.CenterOn(Camera.PanAt(camera.Center, target, 1));
        var middle = new SKPoint(camera.Viewport.Width / 2, camera.Viewport.Height / 2);
        var at = camera.ToScreen(target);
        Assert.Equal(middle.X, at.X, 3);
        Assert.Equal(middle.Y, at.Y, 3);
        Assert.Equal(2.5, camera.Zoom);
    }

    [Fact]
    public void The_edge_badge_waits_while_the_view_glides_to_the_position()
    {
        var (camera, scene) = Of([]);
        scene.Player = new PlayerFix(new WorldPoint(2000, 0, 0), null, DateTime.Now);
        scene.PingSince = DateTime.Now;
        Assert.NotNull(MapRenderer.EdgeOf(camera, scene, 1));

        SKBitmap Draw()
        {
            var bitmap = new SKBitmap(1000, 1000);
            using var canvas = new SKCanvas(bitmap);
            MapRenderer.Render(canvas, camera, scene, 1);
            return bitmap;
        }
        scene.Gliding = true;
        using var gliding = Draw();
        scene.Gliding = false;
        using var still = Draw();
        scene.Player = null;
        using var empty = Draw();
        // Gliding, the view is as if there were no badge; still, the badge is there.
        Assert.True(empty.Pixels.SequenceEqual(gliding.Pixels));
        Assert.False(empty.Pixels.SequenceEqual(still.Pixels));
    }
}
