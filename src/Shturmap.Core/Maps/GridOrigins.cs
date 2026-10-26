namespace Shturmap.Core.Maps;

/// <summary>
/// Each map's A1, fixed in the app (owner, 2026-10-26; docs/DESIGN.md §4, "Map drawing", *The grid*): the top-left corner
/// of the 100 m square that held the map's bounds' left and top edge as drawn, in world metres (x, z), worked out once
/// from tarkov.dev's maps.json of 2026-10-26 (<see cref="SquadGrid.FromBounds"/>) and frozen here as numbers. So every
/// install says the same reference for a place, whatever bounds its copy of the data has, and a reference said today
/// still means the same place after tarkov.dev changes them. Keyed by the map's key in maps.json (its alternate maps,
/// Night Factory, Ground Zero 21+ and The Lab's dark one, are drawn with the same definition and grid). A map tarkov.dev
/// adds starts at its bounds until it gets its entry here (docs/UPDATES.md; <c>shturmap-cli grid</c> lists them);
/// <c>GridOriginsTests</c> hold the table to these numbers.
/// </summary>
public static class GridOrigins
{
    public static IReadOnlyDictionary<string, (double X, double Z)> Frozen { get; } = new Dictionary<string, (double X, double Z)>(StringComparer.Ordinal)
    {
        // Columns across and rows down that cover the bounds of 2026-10-26 in the comments: none more than 17 of the 24.
        ["customs"] = (700, -400),            // 11 x 7, A1 to L7
        ["factory"] = (100, 100),             // 2 x 2, A1 to B2; Night Factory too
        ["ground-zero"] = (300, -200),        // 4 x 6, A1 to D6; Ground Zero 21+ too
        ["icebreaker"] = (100, -100),         // 2 x 2, A1 to B2 (its bounds are Factory's numbers); oblong squares
        ["interchange"] = (600, -500),        // 11 x 10, A1 to L10
        ["lighthouse"] = (600, -1000),        // 12 x 18, A1 to M18
        ["reserve"] = (300, -300),            // 7 x 6, A1 to G6
        ["shoreline"] = (600, -500),          // 17 x 12, A1 to S12
        ["streets-of-tarkov"] = (400, -300),  // 7 x 9, A1 to G9
        ["terminal"] = (500, -600),           // 10 x 11, A1 to K11
        ["the-lab"] = (-300, -500),           // 4 x 3, A1 to D3; The Lab's dark one too
        ["the-labyrinth"] = (-100, -100),     // 2 x 2, A1 to B2
        ["woods"] = (700, -1000),             // 15 x 15, A1 to Q15
    };
}
