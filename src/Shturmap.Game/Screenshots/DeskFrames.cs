using SkiaSharp;

namespace Shturmap.Game.Screenshots;

/// <summary>
/// The compartments of the GEAR tab's middle column and of a container's window: rectangles of whole cells framed by the
/// game's light line on all four sides. A rig, the pockets and the special slots are many small compartments a few
/// pixels apart, not one lattice, so the stash's lattice rule (<see cref="StashGrids"/>) doesn't find them; a framed
/// rectangle does, whatever lies in it (an item covers the lines between its cells, never the compartment's frame).
/// Where a window lies over a compartment, its frame there is under the window: only the frame's visible parts count.
/// </summary>
internal static class DeskFrames
{
    // How much of a side's visible length must show the line.
    private const double Framed = 0.7;

    // The largest compartment looked for, in cells (a backpack's grid; the stash is found as the stash).
    private const int MostCols = 12;
    private const int MostRows = 14;

    // How far a side may stand off the pitch's multiple: the cell is 84.2 px at 1440p, not 84.
    private const int Slack = 3;

    /// <summary>The framed compartments within <paramref name="area"/>, each the largest framed rectangle it is (one framed
    /// inside another is a part of it), top row first.</summary>
    /// <param name="over">The windows lying over the picture: lines under them aren't asked for.</param>
    /// <param name="under">Only the frames a window lies over, their top left corner outside it (the second look, once the
    /// windows are known: their own frames are whole, and found by the first).</param>
    /// <param name="skip">Corners not to look from (the stash's own lattice: the stash is found as the stash).</param>
    /// <param name="open">Where a compartment may run past the area's foot (the GEAR tab's middle column, which scrolls): a
    /// rectangle framed on its top and both its sides down to the foot is a compartment the foot cuts, as its rows in view
    /// (<see cref="OpenAt"/>).</param>
    /// <param name="cutFound">Gets the compartments found so, cut by the foot.</param>
    public static List<SKRectI> Find(StashLines lines, int pitch, SKRectI area, IReadOnlyList<SKRectI> over, bool under = false, Func<int, int, bool>? skip = null,
        SKRectI? open = null, ISet<SKRectI>? cutFound = null)
    {
        // Corners: a line going right and one going down from it, for a cell's side at least.
        var corners = new List<(int X, int Y, double Score)>();
        // Under windows, a frame's corner stands within the largest frame's reach to the left of and above a window.
        var near = under ? over.Select(o => SKRectI.Intersect(area, new SKRectI(o.Left - MostCols * pitch, o.Top - MostRows * pitch, o.Right, o.Bottom))).ToList() : [area];
        for (var y = area.Top; y + pitch < area.Bottom; y++)
        {
            for (var x = area.Left; x + pitch < area.Right; x++)
            {
                if (under && (!near.Any(n => n.Contains(x, y)) || over.Any(o => o.Contains(x, y))))
                    continue;
                if (skip?.Invoke(x, y) == true)
                    continue;
                var top = Share(lines, true, y, x + 2, x + pitch - 2, over);
                if (top < Framed)
                    continue;
                var left = Share(lines, false, x, y + 2, y + pitch - 2, over);
                if (left < Framed)
                    continue;
                corners.Add((x, y, top + left));
            }
        }
        corners.Sort((a, b) => b.Score.CompareTo(a.Score));
        var kept = new List<(int X, int Y)>();
        foreach (var c in corners)
        {
            if (!kept.Any(k => Math.Abs(k.X - c.X) <= 2 && Math.Abs(k.Y - c.Y) <= 2))
                kept.Add((c.X, c.Y));
        }
        var found = new List<SKRectI>();
        // Top left first: a corner inside a frame found is one of its cells, and frames nothing new.
        foreach (var (x, y) in kept.OrderBy(k => k.Y).ThenBy(k => k.X))
        {
            if (found.Any(f => x > f.Left + Slack && x < f.Right - Slack && y > f.Top + Slack && y < f.Bottom - Slack))
                continue;
            // The largest framed rectangle from this corner: its right and bottom sides on lines, near the pitch's multiples.
            // The sides are checked for every size first; the gap between compartments only for the largest so framed.
            var framed = new List<SKRectI>();
            for (var rows = MostRows; rows >= 1; rows--)
            {
                for (var cols = MostCols; cols >= 1; cols--)
                {
                    // Under windows only a rectangle that reaches one is looked for.
                    if (under && !over.Any(o => x < o.Right && x + cols * pitch > o.Left && y < o.Bottom && y + rows * pitch > o.Top))
                        continue;
                    if (FramedAt(lines, x, y, cols, rows, pitch, over, area) is { } rect)
                        framed.Add(rect);
                }
            }
            // A compartment the foot cuts (the middle column scrolled; the test set's 11-gear.png and 12-gear.png:
            // the backpack's grid, empty, ran out of view at the foot, its lines between the empty cells too faint to frame
            // a rectangle, and the backpack read as its slot alone): framed on its top and both sides down to the foot.
            var cut = new HashSet<SKRectI>();
            if (open is { } band && band.Contains(x, y) && OpenEnd(lines, x, y, pitch, over, area) is { } end)
            {
                for (var cols = MostCols; cols >= 1; cols--)
                {
                    if (under && !over.Any(o => x < o.Right && x + cols * pitch > o.Left && y < o.Bottom && end > o.Top))
                        continue;
                    if (OpenAt(lines, x, y, cols, end, pitch, over, area) is { } rect && rect.Right <= band.Right + Slack)
                    {
                        framed.Add(rect);
                        cut.Add(rect);
                    }
                }
            }
            // A cut compartment's bottom is a line between its rows, or none: two compartments side by side break its top line
            // alone.
            SKRectI? best = framed.OrderByDescending(Area).Cast<SKRectI?>()
                .FirstOrDefault(r => (cut.Contains(r!.Value) ? !Gap(lines, r.Value.Top, r.Value.Left, r.Value.Right, over)
                                         : !Split(lines, true, r.Value.Top, r.Value.Bottom, r.Value.Left, r.Value.Right, over)) &&
                                     !Split(lines, false, r.Value.Left, r.Value.Right, r.Value.Top, r.Value.Bottom, over));
            if (under && best is { } seen && !over.Any(o => o.IntersectsWith(seen)))
                continue;
            if (best is { } b && !found.Any(f => Inside(b, f)))
            {
                found.RemoveAll(f => Inside(f, b));
                found.Add(b);
                if (cut.Contains(b))
                    cutFound?.Add(b);
            }
        }
        return Largest(found);
    }

    /// <summary>The frames less those that are part of a bigger one: a rectangle inside a bigger one (two empty cells side
    /// by side frame a rectangle of their own), or mostly over a bigger one (found on the cyan outline a few pixels off
    /// the line); top row first.</summary>
    public static List<SKRectI> Largest(List<SKRectI> found) =>
        found.Distinct().Where(f => !found.Any(o => o != f && (Inside(f, o) && Area(o) >= Area(f) || (Area(o) > Area(f) && Area(SKRectI.Intersect(f, o)) > Area(f) / 2))))
            .OrderBy(f => f.Top).ThenBy(f => f.Left).ToList();

    private static SKRectI? FramedAt(StashLines lines, int x, int y, int cols, int rows, int pitch, IReadOnlyList<SKRectI> over, SKRectI area)
    {
        int right0 = x + cols * pitch, bottom0 = y + rows * pitch;
        if (right0 > area.Right + Slack || bottom0 > area.Bottom + Slack)
            return null;
        // The right side, then the bottom: each the best place within the slack.
        int right = -1, bottom = -1;
        double rightShare = 0, bottomShare = 0;
        for (var d = -Slack; d <= Slack; d++)
        {
            var s = Share(lines, false, right0 + d, y + 2, bottom0 - 2, over);
            if (s > rightShare)
            {
                rightShare = s;
                right = right0 + d;
            }
            var t = Share(lines, true, bottom0 + d, x + 2, right0 - 2, over);
            if (t > bottomShare)
            {
                bottomShare = t;
                bottom = bottom0 + d;
            }
        }
        if (rightShare < Framed || bottomShare < Framed)
            return null;
        if (Share(lines, false, x, y + 2, bottom - 2, over) < Framed || Share(lines, true, y, x + 2, right - 2, over) < Framed)
            return null;
        // (Two compartments a few pixels apart frame a rectangle across both: the gap between them breaks the top and the
        // bottom line at the same place, or the left and the right one; Split, asked of the largest framed only.)
        // A frame mostly under a window is no frame seen.
        var visible = Visible(false, right, y, bottom, over) + Visible(true, bottom, x, right, over) + Visible(false, x, y, bottom, over) + Visible(true, y, x, right, over);
        if (visible < 0.4 * 2 * ((right - x) + (bottom - y)))
            return null;
        return new SKRectI(x, y, right, bottom);
    }

    // Where a compartment's left side, from a corner, runs on to the area's foot without a break (a compartment the foot cuts:
    // the middle column's view ends there, about a quarter of a cell above the QUICK USE bar's top): the row its line ends
    // at; null when it breaks off above the foot (a compartment closed above it, or the gap to the next).
    private static int? OpenEnd(StashLines lines, int x, int y, int pitch, IReadOnlyList<SKRectI> over, SKRectI area)
    {
        if (area.Bottom - y < pitch)
            return null;
        var end = y;
        var missing = 0;
        for (var yy = y + 2; yy + 4 <= area.Bottom; yy += 4)
        {
            if (Share(lines, false, x, yy, yy + 4, over) >= 0.5)
            {
                end = yy + 4;
                missing = 0;
            }
            else if ((missing += 4) > 2 * GapWidth)
                break;
        }
        return end >= area.Bottom - pitch / 2 && end - y >= pitch ? end : null;
    }

    // A compartment the foot cuts, from its corner: the top line and the right side framed down to where the left side ends
    // (the foot), as the whole rows in view.
    private static SKRectI? OpenAt(StashLines lines, int x, int y, int cols, int end, int pitch, IReadOnlyList<SKRectI> over, SKRectI area)
    {
        var right0 = x + cols * pitch;
        if (right0 > area.Right + Slack)
            return null;
        int right = -1;
        double rightShare = 0;
        for (var d = -Slack; d <= Slack; d++)
        {
            var s = Share(lines, false, right0 + d, y + 2, end - 2, over);
            if (s > rightShare)
            {
                rightShare = s;
                right = right0 + d;
            }
        }
        if (rightShare < Framed || Share(lines, true, y, x + 2, right - 2, over) < Framed)
            return null;
        var rows = (end - y + Slack) / pitch;
        return rows < 1 || rows > MostRows ? null : new SKRectI(x, y, right, y + rows * pitch);
    }

    // Whether a line (the row `at` from `from` to `to`) shows nothing over a few pixels, away from its ends: the gap between
    // two compartments side by side.
    private static bool Gap(StashLines lines, int at, int from, int to, IReadOnlyList<SKRectI> over)
    {
        for (var p = from + 8; p + GapWidth < to - 8; p++)
        {
            if (Share(lines, true, at, p, p + GapWidth, over) == 0)
                return true;
        }
        return false;
    }

    // The width of the gap between two compartments that is looked for, in pixels: about 8 at 1440p.
    private const int GapWidth = 5;

    // Whether both sides (the top and bottom rows a and b from `from` to `to`, or the left and right columns) show no line
    // over the same few pixels, away from the corners.
    private static bool Split(StashLines lines, bool horizontal, int a, int b, int from, int to, IReadOnlyList<SKRectI> over)
    {
        for (var p = from + 8; p + GapWidth < to - 8; p++)
        {
            if (Share(lines, horizontal, a, p, p + GapWidth, over) == 0 && Share(lines, horizontal, b, p, p + GapWidth, over) == 0)
                return true;
        }
        return false;
    }

    private static bool Inside(SKRectI a, SKRectI b) =>
        a.Left >= b.Left - 2 * Slack && a.Top >= b.Top - 2 * Slack && a.Right <= b.Right + 2 * Slack && a.Bottom <= b.Bottom + 2 * Slack;

    private static long Area(SKRectI r) => r.IsEmpty ? 0 : (long)r.Width * r.Height;

    /// <summary>How much of a side's visible length shows the line: a horizontal side at row <paramref name="at"/> from
    /// <paramref name="from"/> to <paramref name="to"/>, or a vertical one at column <paramref name="at"/>. A side wholly
    /// under windows counts as shown; one with less than a sixth of its length visible too (it says nothing).</summary>
    internal static double Share(StashLines lines, bool horizontal, int at, int from, int to, IReadOnlyList<SKRectI> over)
    {
        if (to <= from)
            return 0;
        if (over.Count == 0)
            return horizontal ? lines.Horizontal(at, from, to) : lines.Vertical(at, from, to);
        Span<(int A, int B)> covered = stackalloc (int, int)[Math.Min(16, over.Count)];
        var n = Covered(horizontal, at, from, to, over, covered);
        if (n == 0)
            return horizontal ? lines.Horizontal(at, from, to) : lines.Vertical(at, from, to);
        double shown = 0, length = 0;
        var a = from;
        for (var k = 0; k <= n; k++)
        {
            var b = k < n ? covered[k].A : to;
            if (b > a)
            {
                shown += (horizontal ? lines.Horizontal(at, a, b) : lines.Vertical(at, a, b)) * (b - a);
                length += b - a;
            }
            if (k < n)
                a = Math.Max(a, covered[k].B);
        }
        return length < (to - from) / 6.0 ? 1 : shown / length;
    }

    // The parts of a side under windows, clipped to it, in order and merged; how many.
    private static int Covered(bool horizontal, int at, int from, int to, IReadOnlyList<SKRectI> over, Span<(int A, int B)> covered)
    {
        var n = 0;
        foreach (var w in over)
        {
            var (lo, hi, across0, across1) = horizontal ? (w.Left, w.Right, w.Top, w.Bottom) : (w.Top, w.Bottom, w.Left, w.Right);
            if (at < across0 || at >= across1 || hi <= from || lo >= to || n == covered.Length)
                continue;
            var item = (Math.Max(lo, from), Math.Min(hi, to));
            var k = n++;
            while (k > 0 && covered[k - 1].A > item.Item1)
            {
                covered[k] = covered[k - 1];
                k--;
            }
            covered[k] = item;
        }
        return n;
    }

    private static double Visible(bool horizontal, int at, int from, int to, IReadOnlyList<SKRectI> over)
    {
        if (to <= from)
            return 0;
        Span<(int A, int B)> covered = stackalloc (int, int)[Math.Max(1, Math.Min(16, over.Count))];
        var n = Covered(horizontal, at, from, to, over, covered);
        double visible = 0;
        var a = from;
        for (var k = 0; k <= n; k++)
        {
            var b = k < n ? covered[k].A : to;
            if (b > a)
                visible += b - a;
            if (k < n)
                a = Math.Max(a, covered[k].B);
        }
        return visible;
    }
}
