using Shturmap.Core.Maps;

namespace Shturmap.Map;

/// <summary>
/// Whether the view follows the player's position (owner, 2026-10-03: "It should be a toggle in the map view and the
/// camera should smooth scroll to the updated player position"), and what the player's own moves of the view do to
/// it: dragging the map or showing the whole map takes the view back, so following stops and the map never fights
/// the player; zooming keeps following, about the player. With no position on the map (between raids, or before
/// a raid's first screenshot) there is nothing to follow and so nothing to take back: moving the map then leaves
/// following as the player set it (review of 2026-10-04: planning switched it off for the next raid).
/// </summary>
public sealed class FollowState
{
    public bool On { get; private set; }

    public void Set(bool on) => On = on;

    /// <summary>The map was dragged: following stops. True when it was on (the toggle must say so).</summary>
    /// <param name="hasPosition">Whether a position is on the map; without one, following stays as it is.</param>
    public bool Dragged(bool hasPosition = true) => hasPosition && Stop();

    /// <summary>The whole map was shown: following stops. True when it was on.</summary>
    /// <param name="hasPosition">Whether a position is on the map; without one, following stays as it is.</param>
    public bool Fitted(bool hasPosition = true) => hasPosition && Stop();

    /// <summary>The map was zoomed: following goes on. True when the view should centre on the player again.</summary>
    public bool Zoomed() => On;

    /// <summary>Where the view's centre goes to follow: the player's last position, or nowhere without one.</summary>
    public static MapPoint? Target(MapScene scene) => scene.Player is { } fix ? scene.Projection.ToMap(fix.Position) : null;

    private bool Stop()
    {
        var was = On;
        On = false;
        return was;
    }
}
