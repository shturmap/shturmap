namespace Shturmap.Game.Screenshots;

/// <summary>Where a grid stands on the screen: the stash at the right, a container window over it, the trader's
/// grid at the left of the trading screen, or a message's attachments at the left of the messages screen.</summary>
internal enum StashGridPlace
{
    Stash,
    Window,
    Left,

    /// <summary>The messages screen's grid of what a message delivers (a trader's items, rewards), ten wide at the left
    /// with a margin to the edge (owner, 2026-10-11: those items "will be on the left hand side of the screen"). Not
    /// the player's until taken, so never read; the stash at the right is read as on any other screen.</summary>
    Message,
}

/// <summary>A grid of cells found in the picture: its top left in pixels, its size in cells, and where it stands.</summary>
internal sealed record StashGrid(int X0, int Y0, int Cols, int Rows, StashGridPlace Place)
{
    public int Area => Cols * Rows;

    public bool Covers(int x, int y, int pitch) => x >= X0 && y >= Y0 && x < X0 + Cols * pitch && y < Y0 + Rows * pitch;
}

/// <summary>A rectangle of cells with one item in it: its cell at the top left and its size in cells. Odd: the cells
/// joined don't make a rectangle (a hover highlight that merged two items, a wide item cut into cells).</summary>
internal sealed record StashRect(int I0, int J0, int W, int H, bool Odd);

/// <summary>
/// Finds the game's item grids in a menu screenshot by their lines, and the item rectangles in them. The game draws a
/// thin light line between cells; a cell framed on all four sides is a lattice point, cells a pitch apart make a
/// lattice, and lattices of one phase make a grid (a row of wide items leaves no framed cell and would split it).
/// The stash is ten cells wide and stands at the right; a page of large items frames too few cells for a lattice, so
/// it is also found from its lines alone (the column lines repeat every cell, and its last column is the rightmost
/// of that phase that fits on the screen).
/// </summary>
internal static class StashGrids
{
    // How much of a cell's side must show the line for the cell to count as framed.
    private const double Framed = 0.7;

    // The stash's width in cells.
    public const int StashColumns = 10;

    /// <summary>
    /// The grids in the picture, the biggest first: the ones the framed cells give, and the stash from its lines,
    /// which also gives the rows of large items the cells don't frame. w and h are the picture's size, pitch the
    /// cell's.
    /// </summary>
    public static List<StashGrid> Find(StashLines lines, StashGray gray, int w, int h, int pitch)
    {
        var found = FromFramedCells(lines, gray, w, h, pitch);
        var framed = found.FirstOrDefault(g => g.Place == StashGridPlace.Stash);
        var byLines = StashFromLines(lines, gray, w, h, pitch, found);
        if (byLines is not null && framed is null)
            found.Add(byLines);
        else if (byLines is not null && SamePhase(framed!.X0 - byLines.X0, framed.Y0 - byLines.Y0, pitch))
        {
            // The same stash seen both ways: the rows of either.
            int y0 = Math.Min(framed.Y0, byLines.Y0), y1 = Math.Max(framed.Y0 + framed.Rows * pitch, byLines.Y0 + byLines.Rows * pitch);
            found.Remove(framed);
            found.Add(framed with { X0 = byLines.X0, Y0 = y0, Rows = (int)Math.Round((y1 - y0) / (double)pitch) });
        }
        // A lattice of the stash's own phase inside it is some of its cells seen as a grid of their own, not a window.
        if (found.FirstOrDefault(g => g.Place == StashGridPlace.Stash) is { } stash)
        {
            found.RemoveAll(g => g.Place == StashGridPlace.Window && SamePhase(g.X0 - stash.X0, g.Y0 - stash.Y0, pitch) &&
                                 g.X0 >= stash.X0 - 2 && g.Y0 >= stash.Y0 - 2 && g.X0 + g.Cols * pitch <= stash.X0 + stash.Cols * pitch + 2 &&
                                 g.Y0 + g.Rows * pitch <= stash.Y0 + stash.Rows * pitch + 2);
        }
        return found.OrderByDescending(g => g.Area).ToList();
    }

    // The stash is ten wide at the right. The trader's grid on the trading screen hangs on the screen's left edge
    // (11 px in at 2560 wide, a seventh of a cell) and stands left of the middle. The messages screen's grid of a
    // message's attachments is ten wide at the left too, but set in from the edge by most of a cell (64 px at 2560,
    // the owner's two screenshots of 2026-10-11), its top level with the stash's. A container window dragged left of
    // the middle but not to the edge is a window (seen 2026-10-11, three nested windows over the gear screen).
    private static StashGridPlace PlaceOf(int x0, int cols, int w, int pitch) =>
        cols == StashColumns && x0 > w / 2 ? StashGridPlace.Stash
        : x0 < pitch / 2 && x0 + cols * pitch < w / 2 ? StashGridPlace.Left
        : cols == StashColumns && x0 < pitch && x0 + cols * pitch < w / 2 ? StashGridPlace.Message
        : StashGridPlace.Window;

    private static List<StashGrid> FromFramedCells(StashLines lines, StashGray gray, int w, int h, int pitch)
    {
        // Every position where a cell of the pitch is framed on all four sides, scored by how well.
        var framed = new List<(int X, int Y, double Score)>();
        for (var y = 0; y + pitch < h; y++)
        {
            for (var x = 0; x + pitch < w; x++)
            {
                var l = lines.Vertical(x, y + 2, y + pitch - 2);
                if (l < Framed)
                    continue;
                var r = lines.Vertical(x + pitch, y + 2, y + pitch - 2);
                if (r < Framed)
                    continue;
                var t = lines.Horizontal(y, x + 2, x + pitch - 2);
                if (t < Framed)
                    continue;
                var b = lines.Horizontal(y + pitch, x + 2, x + pitch - 2);
                if (b < Framed)
                    continue;
                framed.Add((x, y, l + r + t + b));
            }
        }
        // One position per cell: the best within two pixels.
        framed.Sort((a, b) => b.Score.CompareTo(a.Score));
        var cells = new List<(int X, int Y)>();
        foreach (var f in framed)
        {
            if (!cells.Any(c => Math.Abs(c.X - f.X) <= 2 && Math.Abs(c.Y - f.Y) <= 2))
                cells.Add((f.X, f.Y));
        }
        // Cells a pitch apart (give or take two pixels) belong to one lattice.
        var parent = Enumerable.Range(0, cells.Count).ToArray();
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
        for (var i = 0; i < cells.Count; i++)
        {
            for (var j = i + 1; j < cells.Count; j++)
            {
                int dx = Math.Abs(cells[i].X - cells[j].X), dy = Math.Abs(cells[i].Y - cells[j].Y);
                var sameColumn = dx <= 2 && Math.Abs(dy - pitch) <= 2;
                var sameRow = dy <= 2 && Math.Abs(dx - pitch) <= 2;
                if (sameColumn || sameRow)
                    parent[Find(i)] = Find(j);
            }
        }
        var groups = cells.Select((c, i) => (c, Root: Find(i))).GroupBy(x => x.Root).Select(g => g.Select(x => x.c).ToList()).ToList();
        // A row of wide items across a grid (weapons in the stash) leaves no framed cell in it and splits the lattice
        // in two: two lattices of the same phase, one above the other over the same columns (or side by side over the
        // same rows), a few cells apart, are one grid.
        var boxes = groups.Select(g => (X0: g.Min(c => c.X), Y0: g.Min(c => c.Y), X1: g.Max(c => c.X) + pitch, Y1: g.Max(c => c.Y) + pitch, Count: g.Count)).ToList();
        var bParent = Enumerable.Range(0, boxes.Count).ToArray();
        int BFind(int i) => bParent[i] == i ? i : bParent[i] = BFind(bParent[i]);
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var a = boxes[i];
                var b = boxes[j];
                if (!SamePhase(a.X0 - b.X0, a.Y0 - b.Y0, pitch))
                    continue;
                var sharedColumns = Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0) >= pitch - 2;
                var sharedRows = Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0) >= pitch - 2;
                var rowGap = Math.Max(a.Y0, b.Y0) - Math.Min(a.Y1, b.Y1);
                var columnGap = Math.Max(a.X0, b.X0) - Math.Min(a.X1, b.X1);
                if ((sharedColumns && rowGap <= 8 * pitch) || (sharedRows && columnGap <= 3 * pitch))
                    bParent[BFind(i)] = BFind(j);
            }
        }
        var found = new List<(StashGrid Grid, int Count)>();
        foreach (var merged in boxes.Select((b, i) => (b, Root: BFind(i))).GroupBy(x => x.Root).Select(g => g.Select(x => x.b).ToList()))
        {
            int x0 = merged.Min(b => b.X0), y0 = merged.Min(b => b.Y0), x1 = merged.Max(b => b.X1), y1 = merged.Max(b => b.Y1);
            int cols = (int)Math.Round((x1 - x0) / (double)pitch), rows = (int)Math.Round((y1 - y0) / (double)pitch);
            var count = merged.Sum(b => b.Count);
            if (cols < 2 || rows < 2 || count < 4)
                continue;
            (x0, y0) = Snap(gray, x0, y0, cols, rows, pitch);
            found.Add((new StashGrid(x0, y0, cols, rows, PlaceOf(x0, cols, w, pitch)), count));
        }
        // Snapped onto the same lines, two lattices of one grid (one found on the cyan outlines, one on the grey
        // lines, five pixels apart) are one grid.
        for (var i = 0; i < found.Count; i++)
        {
            for (var j = found.Count - 1; j > i; j--)
            {
                var a = found[i].Grid;
                var b = found[j].Grid;
                var overlap = a.X0 < b.X0 + b.Cols * pitch && b.X0 < a.X0 + a.Cols * pitch && a.Y0 < b.Y0 + b.Rows * pitch && b.Y0 < a.Y0 + a.Rows * pitch;
                if (!SamePhase(a.X0 - b.X0, a.Y0 - b.Y0, pitch) || !overlap)
                    continue;
                int x0 = Math.Min(a.X0, b.X0), y0 = Math.Min(a.Y0, b.Y0);
                int x1 = Math.Max(a.X0 + a.Cols * pitch, b.X0 + b.Cols * pitch), y1 = Math.Max(a.Y0 + a.Rows * pitch, b.Y0 + b.Rows * pitch);
                int cols = (int)Math.Round((x1 - x0) / (double)pitch), rows = (int)Math.Round((y1 - y0) / (double)pitch);
                found[i] = (new StashGrid(x0, y0, cols, rows, PlaceOf(x0, cols, w, pitch)), found[i].Count + found[j].Count);
                found.RemoveAt(j);
            }
        }
        // A sparse lattice inside a bigger one is the bigger one's art seen as lines, not a window over it.
        return found
            .Where(f => f.Count >= f.Grid.Area / 4 || !found.Any(o => o.Grid != f.Grid && o.Grid.Area > f.Grid.Area && o.Grid.Covers(f.Grid.X0 + 1, f.Grid.Y0 + 1, pitch)))
            .Select(f => f.Grid)
            .ToList();
    }

    // Whether two lattices stand on the same lines: their offsets are whole cells, give or take two pixels.
    private static bool SamePhase(int dx, int dy, int pitch)
    {
        int phaseX = ((dx % pitch) + pitch) % pitch, phaseY = ((dy % pitch) + pitch) % pitch;
        return (phaseX <= 2 || phaseX >= pitch - 2) && (phaseY <= 2 || phaseY >= pitch - 2);
    }

    // The least of the stash's eleven column lines that must show for the stash to be taken from its lines, and how
    // much of the looked-at height each must show.
    private const int StashLinesNeeded = 6;
    private const double StashLineShare = 0.12;

    // How much of a row's height a column line must show for the row to count as inside the stash (a row the
    // toolbar above the stash cuts by a fifth or more is out: its labels are hidden, and the toolbar's words would be
    // read in their place), and how many column lines must: the outer two frame every row, and any item boundary
    // adds one.
    private const double RowLine = 0.85;
    private const int RowLinesNeeded = 3;

    /// <summary>
    /// The stash from its column lines, when its cells gave no lattice (a page of helmets and cases frames almost
    /// no cell): the phase whose column lines, in the picture's right part, show most, its last column the rightmost
    /// of them that fits on the screen; then the rows, by the row lines' phase and by which rows the column lines
    /// run through. Null when the lines don't show: a screen without the stash, or the stash all under a window.
    /// </summary>
    private static StashGrid? StashFromLines(StashLines lines, StashGray gray, int w, int h, int pitch, List<StashGrid> windows)
    {
        int yTop = h / 20, yBottom = h - h / 20;
        var bestPhase = -1;
        double bestSum = 0;
        var bestShowing = 0;
        for (var phase = 0; phase < pitch; phase++)
        {
            // The rightmost line of this phase that leaves a whole column before it on the screen's right half.
            var last = phase + (w - 1 - phase) / pitch * pitch;
            var first = last - StashColumns * pitch;
            if (first < w / 2)
                continue;
            double sum = 0;
            var showing = 0;
            for (var k = 0; k <= StashColumns; k++)
            {
                var share = lines.Vertical(first + k * pitch, yTop, yBottom);
                sum += share;
                if (share >= StashLineShare)
                    showing++;
            }
            if (showing >= StashLinesNeeded && sum > bestSum)
            {
                bestSum = sum;
                bestPhase = phase;
                bestShowing = showing;
            }
        }
        if (bestPhase < 0 || bestShowing < StashLinesNeeded)
            return null;
        var x0 = bestPhase + (w - 1 - bestPhase) / pitch * pitch - StashColumns * pitch;
        var x1 = x0 + StashColumns * pitch;

        // The row lines' phase: the one whose lines across the stash show most.
        var rowPhase = 0;
        double rowSum = -1;
        for (var phase = 0; phase < pitch; phase++)
        {
            double sum = 0;
            for (var y = phase; y < h; y += pitch)
                sum += lines.Horizontal(y, x0 + 2, x1 - 2);
            if (sum > rowSum)
            {
                rowSum = sum;
                rowPhase = phase;
            }
        }
        // The rows the column lines run through, as the longest such run. A window over the stash hides its lines
        // there, so a row under a window between two rows that show counts as inside.
        var inside = new List<bool>();
        var underWindow = new List<bool>();
        for (var y = rowPhase; y + pitch <= h; y += pitch)
        {
            var showing = 0;
            for (var k = 0; k <= StashColumns; k++)
            {
                if (lines.Vertical(x0 + k * pitch, y + 2, y + pitch - 2) >= RowLine)
                    showing++;
            }
            inside.Add(showing >= RowLinesNeeded);
            underWindow.Add(windows.Any(o => o.Place == StashGridPlace.Window && o.Y0 <= y + pitch / 2 && y + pitch / 2 < o.Y0 + o.Rows * pitch));
        }
        for (var i = 0; i < inside.Count; i++)
        {
            if (!inside[i] && underWindow[i] && inside.Take(i).Any(b => b) && inside.Skip(i + 1).Any(b => b))
                inside[i] = true;
        }
        int bestStart = -1, bestLength = 0;
        for (var i = 0; i < inside.Count; i++)
        {
            if (!inside[i])
                continue;
            var j = i;
            while (j + 1 < inside.Count && inside[j + 1])
                j++;
            if (j - i + 1 > bestLength)
            {
                bestLength = j - i + 1;
                bestStart = i;
            }
            i = j;
        }
        if (bestLength < 2)
            return null;
        var y0 = rowPhase + bestStart * pitch;
        (x0, y0) = Snap(gray, x0, y0, StashColumns, bestLength, pitch);
        return new StashGrid(x0, y0, StashColumns, bestLength, StashGridPlace.Stash);
    }

    // The lattice's top left, moved onto the middle of the light line it stands near: the lines are found with a
    // tolerance that can put the lattice a pixel or two off, and a cell cut two pixels early reads worse.
    internal static (int X0, int Y0) Snap(StashGray gray, int x0, int y0, int cols, int rows, int pitch)
    {
        const int reach = 4, side = 3;
        int y1 = Math.Min(gray.H, y0 + rows * pitch), x1 = Math.Min(gray.W, x0 + cols * pitch);
        var columns = new double[2 * (reach + side) + 1];
        for (var dx = -reach - side; dx <= reach + side; dx++)
        {
            var x = x0 + dx;
            if (x < 0 || x >= gray.W)
                continue;
            double sum = 0;
            for (var y = Math.Max(0, y0 + 4); y < y1 - 4; y++)
                sum += gray[x, y];
            columns[dx + reach + side] = sum / Math.Max(1, y1 - y0 - 8);
        }
        var rowsProfile = new double[2 * (reach + side) + 1];
        for (var dy = -reach - side; dy <= reach + side; dy++)
        {
            var y = y0 + dy;
            if (y < 0 || y >= gray.H)
                continue;
            double sum = 0;
            for (var x = Math.Max(0, x0 + 4); x < x1 - 4; x++)
                sum += gray[x, y];
            rowsProfile[dy + reach + side] = sum / Math.Max(1, x1 - x0 - 8);
        }
        return (x0 + Thinnest(columns, reach, side) - reach, y0 + Thinnest(rowsProfile, reach, side) - reach);
    }

    // The offset within reach where the profile stands out most against both sides a few pixels off: a thin line,
    // not a row of labels (bright over ten pixels) or an item's edge (bright on one side only).
    private static int Thinnest(double[] profile, int reach, int side)
    {
        var best = reach;
        var bestScore = double.MinValue;
        for (var d = 0; d <= 2 * reach; d++)
        {
            var i = d + side;
            var score = profile[i] - Math.Max(profile[i - side], profile[i + side]);
            if (score > bestScore)
            {
                bestScore = score;
                best = d;
            }
        }
        return best;
    }

    /// <summary>Which cells of the grid another grid drawn over it hides: a window hides the stash under it, and a
    /// smaller window a bigger one.</summary>
    public static bool[] Hidden(StashGrid grid, IReadOnlyList<StashGrid> grids, int pitch)
    {
        var over = grids.Where(o => o != grid && o.Place == StashGridPlace.Window && (grid.Place == StashGridPlace.Stash || o.Area < grid.Area)).ToList();
        var hidden = new bool[grid.Area];
        for (var j = 0; j < grid.Rows; j++)
        {
            for (var i = 0; i < grid.Cols; i++)
            {
                int cx = grid.X0 + i * pitch + pitch / 2, cy = grid.Y0 + j * pitch + pitch / 2;
                hidden[j * grid.Cols + i] = over.Any(o => o.Covers(cx, cy, pitch));
            }
        }
        return hidden;
    }

    /// <summary>Which cells are empty: the game's dark field, nothing but its hatch once the gradient is taken out.</summary>
    public static bool[] Empty(StashGrid grid, StashGray highPassed, int pitch)
    {
        var empty = new bool[grid.Area];
        var m = pitch / 8;
        for (var j = 0; j < grid.Rows; j++)
        {
            for (var i = 0; i < grid.Cols; i++)
            {
                int x = grid.X0 + i * pitch, y = grid.Y0 + j * pitch;
                empty[j * grid.Cols + i] = highPassed.Spread(x + m, y + m, x + pitch - m, y + pitch - m) < 3.5;
            }
        }
        return empty;
    }

    /// <summary>The items in the grid as rectangles of cells: cells with something in them, joined across a missing
    /// line (a wide or tall item covers the lines between its cells), top row first. An item whose art fills its
    /// cell up to the line hides the line on its side, and two such neighbours are joined (a one-sided line rule
    /// was tried on 2026-10-11 and split magazines on their own art's edges instead).</summary>
    public static List<StashRect> Items(StashGrid grid, StashLines lines, bool[] hidden, bool[] empty, int pitch)
    {
        var parent = Enumerable.Range(0, grid.Area).ToArray();
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
        for (var j = 0; j < grid.Rows; j++)
        {
            for (var i = 0; i < grid.Cols; i++)
            {
                var k = j * grid.Cols + i;
                if (hidden[k] || empty[k])
                    continue;
                int cx = grid.X0 + i * pitch, cy = grid.Y0 + j * pitch;
                if (i + 1 < grid.Cols && !hidden[k + 1] && !empty[k + 1] && lines.Vertical(cx + pitch, cy + 4, cy + pitch - 4) < 0.4)
                    parent[Find(k)] = Find(k + 1);
                if (j + 1 < grid.Rows && !hidden[k + grid.Cols] && !empty[k + grid.Cols] && lines.Horizontal(cy + pitch, cx + 4, cx + pitch - 4) < 0.4)
                    parent[Find(k)] = Find(k + grid.Cols);
            }
        }
        return Enumerable.Range(0, grid.Area).Where(k => !hidden[k] && !empty[k]).GroupBy(Find)
            .Select(g =>
            {
                int i0 = g.Min(k => k % grid.Cols), j0 = g.Min(k => k / grid.Cols);
                int w = g.Max(k => k % grid.Cols) - i0 + 1, h = g.Max(k => k / grid.Cols) - j0 + 1;
                return new StashRect(i0, j0, w, h, g.Count() != w * h || w > 6 || h > 6);
            })
            .OrderBy(r => r.J0).ThenBy(r => r.I0).ToList();
    }
}
