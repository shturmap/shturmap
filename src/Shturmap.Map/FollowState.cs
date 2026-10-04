using Shturmap.Core.Maps;

namespace Shturmap.Map;

/// <summary>
/// Whether the view follows the player's position (owner, 2026-10-03: "It should be a toggle in the map view and the
/// camera should smooth scroll to the updated player position"), and what the player's own moves of the view do to
/// it. Only the toggle turns following on or off (owner, 2026-10-04: "when you have the follow on you should be able
/// to drag and zoom. When a position is updated, it should center back to the player but at the current zoom
/// level"; until then a drag or showing the whole map switched it off, for the next raids too). Dragging the map or
/// showing the whole map takes the view away for the moment: the next position brings it back, at the zoom the view
/// then has. Zooming keeps the player in the middle while the view is on the player, and is free once it is away.
/// </summary>
public sealed class FollowState
{
    public bool On { get; private set; }

    /// <summary>
    /// The player has moved the view off the position since it was last centred there: zooming no longer pulls it
    /// back, the next position does.
    /// </summary>
    public bool Away { get; private set; }

    public void Set(bool on)
    {
        On = on;
        Away = false;
    }

    /// <summary>The map was dragged: the view is the player's until the next position. Following stays on.</summary>
    public void Dragged() => Away = On;

    /// <summary>The whole map was shown: as a drag, the view is the player's until the next position.</summary>
    public void Fitted() => Away = On;

    /// <summary>The view was centred on the position (a new position arrived, or the player asked to see it).</summary>
    public void Centred() => Away = false;

    /// <summary>The map was zoomed. True when the view should centre on the player again: following, and not away.</summary>
    public bool Zoomed() => On && !Away;

    /// <summary>Where the player is on the map: the player's last position, or nowhere without one.</summary>
    public static MapPoint? Target(MapScene scene) => scene.Player is { } fix ? scene.Projection.ToMap(fix.Position) : null;

    /// <summary>
    /// How far ahead of the player the middle of the view lies, as a share of the view's size that way: a sixth, so the
    /// player stands a third of the way in from the edge behind and two thirds of the view lie ahead (the rule of
    /// thirds; a quarter put the player too near the edge to see what is beside and behind).
    /// </summary>
    public const double Lead = 1.0 / 6;

    /// <summary>
    /// Where the view's centre goes to show the player: ahead of the position, the way the player faces, so more of
    /// the map lies where they are looking (owner, 2026-10-04: "it should not dead-center on the player, but rather
    /// show more of the map in the area where the player is looking"). Only while the facing is still shown
    /// (<see cref="Shturmap.Core.Navigation.Facing.Fresh"/>): an old position is put in the middle, since nothing
    /// says any more which way the player looks.
    /// </summary>
    public static MapPoint? Target(MapScene scene, Camera camera, DateTime now)
    {
        if (scene.Player is not { } fix)
            return null;
        var at = scene.Projection.ToMap(fix.Position);
        if (fix.YawDegrees is not { } yaw || Shturmap.Core.Logs.WallClock.Elapsed(fix.At, now) >= Shturmap.Core.Navigation.Facing.Fresh)
            return at;
        // Clockwise from "up" on the screen; map units grow to the right and downward, a pixel is 1 / zoom of them.
        var heading = scene.Projection.ScreenHeadingDegrees(fix.Position, yaw) * Math.PI / 180;
        return new MapPoint(
            at.X + Math.Sin(heading) * camera.Viewport.Width * Lead / camera.Zoom,
            at.Y - Math.Cos(heading) * camera.Viewport.Height * Lead / camera.Zoom);
    }
}
