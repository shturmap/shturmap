namespace Shturmap.App.Rules;

/// <summary>
/// Which map's scene the map view holds, and which one is on its way (review of 2026-10-04, A32). A map's artwork
/// arrives a moment after the map is asked for. Until then the view still holds the scene of the map before, and a
/// snapshot that came in that moment, already about the new map, was written into it: the old map's picture under
/// the new map's markers and position. So a snapshot fills a scene only when it is the scene of its own map, and a
/// map counts as shown only once its scene is in the view.
/// </summary>
public sealed class SceneGate
{
    /// <summary>The map whose scene is in the view, or null: none yet, or another map's for a preview.</summary>
    public string? Shown { get; private set; }

    /// <summary>The map whose artwork is being fetched, or null.</summary>
    public string? Loading { get; private set; }

    /// <summary>
    /// A snapshot is about this map. True when its scene has to be made now: fetch the artwork, then ask
    /// <see cref="Arrived"/>. False when its scene is in the view already or its artwork is on its way. Going back to
    /// the map in the view drops whatever was on its way.
    /// </summary>
    public bool Wants(string map)
    {
        if (map == Shown)
        {
            Loading = null;
            return false;
        }
        if (map == Loading)
            return false;
        Loading = map;
        return true;
    }

    /// <summary>A map's artwork arrived. True when that map is still the one wanted: its scene goes into the view now.</summary>
    public bool Arrived(string map)
    {
        if (map != Loading)
            return false;
        Loading = null;
        Shown = map;
        return true;
    }

    /// <summary>The view holds something else now (another map, previewed), or its scene must be made anew.</summary>
    public void Forget()
    {
        Shown = null;
        Loading = null;
    }

    /// <summary>Whether a snapshot about this map may write into the scene in the view.</summary>
    public bool Holds(string? map) => map is not null && map == Shown;
}
