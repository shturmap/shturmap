using System.Globalization;

namespace Shturmap.Core.Maps;

/// <summary>
/// A square of the squad grid, and the third of it a point lies in: "D5", "D5-8" (<see cref="SquadGrid"/>).
/// </summary>
/// <param name="Column">0 for A, 1 for B, … 23 for Z (I and O left out: <see cref="SquadGrid.Letters"/>).</param>
/// <param name="Row">1 for the top row as drawn, counting down.</param>
/// <param name="Keypad">The ninth of the square, as on a computer's number pad: 7 8 9 on the top row, 4 5 6 in the
/// middle, 1 2 3 on the bottom; null for the square alone.</param>
public readonly record struct GridReference(int Column, int Row, int? Keypad = null)
{
    /// <summary>The square alone: "D5".</summary>
    public string Square => SquadGrid.Letter(Column) + Row.ToString(CultureInfo.InvariantCulture);

    /// <summary>"D5-8", or "D5" without a keypad digit: the same in every language, as players say it.</summary>
    public override string ToString() =>
        Keypad is { } digit ? Square + "-" + digit.ToString(CultureInfo.InvariantCulture) : Square;
}

/// <summary>
/// The squad grid (owner, 2026-10-26, from a panel of five: E; docs/DESIGN.md §4, "Map drawing", *The grid*): squares of
/// 100 m on the map as drawn, its columns lettered A to Z from the left without I and O (they read as 1 and 0), its rows
/// numbered from 1 at the top, and each square cut in thirds, its ninths named as on a computer's number pad (7 8 9 on the
/// top row, 1 2 3 on the bottom), so a teammate's "Delta 5-8" is found by reading, not counting.
/// <para>The lines lie on world multiples of 100 m and their thirds: every map's coordinateRotation is a multiple of 90°
/// (tarkov.dev's maps.json, October 2026), so the map's axes on screen are the world's x and z, each one way or the other
/// (<see cref="Along"/>). Each map's A1 is fixed in the app (<see cref="GridOrigins"/>), never worked out at run time
/// from tarkov.dev's bounds, which can change: a reference is only worth saying if every player's Shturmap draws the same
/// grid. A map without an entry (one tarkov.dev added since) starts at its bounds (<see cref="Fixed"/> false).</para>
/// </summary>
public sealed class SquadGrid
{
    /// <summary>A square's side, in world metres.</summary>
    public const double Square = 100;

    /// <summary>A third of a square's side: the keypad's lines.</summary>
    public const double Third = Square / 3;

    /// <summary>The columns' letters, A to Z without I and O: 24 columns at most.</summary>
    public const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    // The map's world axes along the screen's: U = su·rx metres to the right, V = sv·ry metres down, rx and ry the
    // world point rotated by coordinateRotation (MapProjection), su and sv the signs tarkov.dev's transform gives them.
    private readonly double _cos, _sin, _su, _sv;

    private SquadGrid(MapDefinition map, (double X, double Z)? frozen)
    {
        var radians = map.CoordinateRotation * Math.PI / 180;
        // Whole on a multiple of 90°, so a line on a world multiple of 100 m is one on a multiple of 100 here, exactly.
        _cos = Whole(Math.Cos(radians));
        _sin = Whole(Math.Sin(radians));
        _su = map.Transform[0] < 0 ? -1 : 1;
        _sv = map.Transform[2] < 0 ? 1 : -1;
        var (u1, v1) = Along(map.Bounds.X1, map.Bounds.Z1);
        var (u2, v2) = Along(map.Bounds.X2, map.Bounds.Z2);
        var (left, top, right, bottom) = (Math.Min(u1, u2), Math.Min(v1, v2), Math.Max(u1, u2), Math.Max(v1, v2));
        if (frozen is { } corner)
        {
            (OriginU, OriginV) = Along(corner.X, corner.Z);
            Fixed = true;
        }
        else
        {
            // The 100 m square that holds the bounds' left and top edge as drawn.
            OriginU = Math.Floor(left / Square) * Square;
            OriginV = Math.Floor(top / Square) * Square;
        }
        Columns = Math.Max(1, (int)Math.Ceiling((right - OriginU) / Square - 1e-9));
        Rows = Math.Max(1, (int)Math.Ceiling((bottom - OriginV) / Square - 1e-9));
    }

    /// <summary>The grid of a map: A1 from <see cref="GridOrigins"/>, or, for a map without an entry, from its bounds.</summary>
    public static SquadGrid For(MapDefinition map) =>
        new(map, GridOrigins.Frozen.TryGetValue(map.Key, out var corner) ? corner : null);

    /// <summary>The grid a map's bounds give it, without its frozen A1: how <see cref="GridOrigins"/> was made, and what a
    /// new map gets.</summary>
    public static SquadGrid FromBounds(MapDefinition map) => new(map, null);

    /// <summary>Whether A1 is the app's own for this map (<see cref="GridOrigins"/>), not worked out from its bounds.</summary>
    public bool Fixed { get; }

    /// <summary>A1's left edge, in metres along the map's horizontal (<see cref="Along"/>): a multiple of 100.</summary>
    public double OriginU { get; }

    /// <summary>A1's top edge, in metres along the map's vertical (<see cref="Along"/>): a multiple of 100.</summary>
    public double OriginV { get; }

    /// <summary>The squares across that cover the map's bounds, from A.</summary>
    public int Columns { get; }

    /// <summary>The squares down that cover the map's bounds, from 1.</summary>
    public int Rows { get; }

    /// <summary>A1's top-left corner in world metres: what <see cref="GridOrigins"/> holds per map.</summary>
    public (double X, double Z) Corner => World(OriginU, OriginV);

    /// <summary>A world point in the grid's metres: U to the right on the map as drawn, V down.</summary>
    public (double U, double V) Along(double x, double z)
    {
        var rx = x * _cos - z * _sin;
        var ry = x * _sin + z * _cos;
        return (_su * rx, _sv * ry);
    }

    /// <summary>The world point (x, z) at the grid's metres U to the right and V down.</summary>
    public (double X, double Z) World(double u, double v)
    {
        var (rx, ry) = (u * _su, v * _sv);
        return (rx * _cos + ry * _sin, -rx * _sin + ry * _cos);
    }

    /// <summary>
    /// The square and the ninth a world point lies in ("D5-8"), or null outside the lettered grid: left of A, past Z, or
    /// above row 1. Past the map's last row or column inside A to Z it goes on ("D11" on a map of ten rows), so a
    /// position just past tarkov.dev's bounds still has one.
    /// </summary>
    public GridReference? At(double x, double z)
    {
        var (u, v) = Along(x, z);
        var (across, down) = (u - OriginU, v - OriginV);
        var column = (int)Math.Floor(across / Square);
        var row = (int)Math.Floor(down / Square);
        if (column < 0 || column >= Letters.Length || row < 0)
            return null;
        var third = (int)Math.Clamp(Math.Floor((across - column * Square) / Third), 0, 2);
        var thirdDown = (int)Math.Clamp(Math.Floor((down - row * Square) / Third), 0, 2);
        return new GridReference(column, row + 1, Keypad(third, thirdDown));
    }

    public GridReference? At(WorldPoint p) => At(p.X, p.Z);

    /// <summary>A column's letter: A for 0, H for 7, J for 8 (no I), N for 12, P for 13 (no O), Z for 23; "?" outside them.</summary>
    public static string Letter(int column) =>
        column >= 0 && column < Letters.Length ? Letters[column].ToString() : "?";

    /// <summary>
    /// The ninth of a square as on a computer's number pad: <paramref name="across"/> 0 to 2 from the left,
    /// <paramref name="down"/> 0 to 2 from the top; 7 at the top left, 9 at the top right, 1 at the bottom left, 3 at the
    /// bottom right (a phone's keypad has 1 2 3 on top; players' keypads are a keyboard's).
    /// </summary>
    public static int Keypad(int across, int down) => (2 - down) * 3 + across + 1;

    private static double Whole(double v) => Math.Abs(v - Math.Round(v)) < 1e-9 ? Math.Round(v) : v;
}
