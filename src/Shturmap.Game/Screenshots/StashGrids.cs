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
internal sealed record StashRect(int I0, int J0, int W, int H, bool Odd)
{
    /// <summary>An odd shape's own cells, as (i, j) in the grid (its bounding box may take others' cells).</summary>
    public IReadOnlyList<(int I, int J)> Cells { get; init; } = [];

    /// <summary>Where the rectangle was joined across faint lines (<see cref="StashGrids.Items"/>): the pieces the lines
    /// that show give, for a reader whose label names no item of the rectangle's size (two items whose frame between them
    /// stood out faintly, a case over a vest); else none.</summary>
    public IReadOnlyList<StashRect> Pieces { get; init; } = [];
}

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
    /// cell's. <paramref name="stashPane"/> false: no stash stands on the screen (the raid's inventory screen with its LOOT
    /// pane where the stash pane would be, <see cref="DeskLayout"/>): the framed cells' grids alone, each a window's.
    /// </summary>
    public static List<StashGrid> Find(StashLines lines, StashGray gray, int w, int h, int pitch, bool stashPane = true)
    {
        var found = FromFramedCells(lines, gray, w, h, pitch);
        if (!stashPane)
        {
            return found.Select(g => g.Place == StashGridPlace.Stash ? g with { Place = StashGridPlace.Window } : g)
                .Select(g => g.Place == StashGridPlace.Window ? Aligned(lines, g, pitch) : g).OrderByDescending(g => g.Area).ToList();
        }
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
        // A window's lattice on its cells' lines, not on its outline.
        return found.Select(g => g.Place == StashGridPlace.Window ? Aligned(lines, g, pitch) : g).OrderByDescending(g => g.Area).ToList();
    }

    /// <summary>
    /// A window's lattice moved onto its cells' lines: the offset within a sixth of a cell either way where the lines
    /// between its cells show most. The grid finding snaps a lattice onto the light line nearest its corner, and a window's
    /// own outline, a thin bright line a few pixels outside its first cells, can take it there: the Keys window of
    /// 2026-10-19 (20:22, after the game's SORT) stood 6 px left of its cells, every line between them missed, and its 71
    /// keys read as one odd shape. The lines between the cells decide, not the outer ones (the outline is one). Done for
    /// every window <see cref="Find"/> gives, so the stash desk and the quest marks read one lattice (the reader round of
    /// 2026-10-19: the quest marks found 23 and 28 of the window's 72 keys before).
    /// </summary>
    internal static StashGrid Aligned(StashLines lines, StashGrid grid, int pitch)
    {
        int cols = grid.Cols, rows = grid.Rows;
        if (cols < 2 && rows < 2)
            return grid;
        var reach = Math.Max(2, pitch / 6);
        double Columns(int x0, int y0)
        {
            double sum = 0;
            for (var k = 1; k < cols; k++)
                sum += lines.Vertical(x0 + k * pitch, y0 + 2, y0 + rows * pitch - 2);
            return sum;
        }
        double Rows(int x0, int y0)
        {
            double sum = 0;
            for (var k = 1; k < rows; k++)
                sum += lines.Horizontal(y0 + k * pitch, x0 + 2, x0 + cols * pitch - 2);
            return sum;
        }
        // Each way on its own: the columns' lines set the left edge, the rows' the top. A move needs clearly more lines
        // than where it stands (a lattice already on its lines stays put).
        int Best(Func<int, double> score)
        {
            var scores = Enumerable.Range(-reach, 2 * reach + 1).Select(score).ToArray();
            var here = scores[reach];
            var top = scores.Max();
            if (top <= here * 1.25 + 0.5)
                return 0;
            // A line is found a pixel either side of it (StashLines): the middle of the run of offsets that see it best.
            var start = Array.FindIndex(scores, s => s >= top - 0.01);
            var end = start;
            while (end + 1 < scores.Length && scores[end + 1] >= top - 0.01)
                end++;
            return (start + end) / 2 - reach;
        }
        var dx = cols > 1 ? Best(d => Columns(grid.X0 + d, grid.Y0)) : 0;
        var dy = rows > 1 ? Best(d => Rows(grid.X0 + dx, grid.Y0 + d)) : 0;
        return grid with { X0 = grid.X0 + dx, Y0 = grid.Y0 + dy };
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
    // tolerance that can put the lattice a pixel or two off, and a cell cut two pixels early reads worse. Never left of
    // minX or above minY (a window's compartment stays inside its outline, which is brighter than its grid's line).
    internal static (int X0, int Y0) Snap(StashGray gray, int x0, int y0, int cols, int rows, int pitch, int minX = int.MinValue, int minY = int.MinValue)
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
        var leastX = minX == int.MinValue ? 0 : Math.Max(0, minX - (x0 - reach));
        var leastY = minY == int.MinValue ? 0 : Math.Max(0, minY - (y0 - reach));
        return (x0 + Thinnest(columns, reach, side, leastX) - reach, y0 + Thinnest(rowsProfile, reach, side, leastY) - reach);
    }

    // The offset within reach where the profile stands out most against both sides a few pixels off: a thin line,
    // not a row of labels (bright over ten pixels) or an item's edge (bright on one side only); from the least offset
    // allowed.
    private static int Thinnest(double[] profile, int reach, int side, int least = 0)
    {
        var best = Math.Clamp(reach, least, 2 * reach);
        var bestScore = double.MinValue;
        for (var d = Math.Min(least, 2 * reach); d <= 2 * reach; d++)
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

    /// <summary>
    /// The items in the grid as rectangles of cells: cells with something in them, joined across a missing line (a wide or
    /// tall item covers the lines between its cells), top row first. An item whose art fills its cell up to the line hides
    /// the line on its side, and two such neighbours are joined (a one-sided line rule was tried on 2026-10-11 and split
    /// magazines on their own art's edges instead). A line that shows but stands out faintly is the grid's own seen through
    /// an item's background, not an item's frame (<see cref="Faint"/>), and a cell read as empty inside an item is its dark
    /// art (a gun's receiver, a scope's tube): the cells joined across such lines, with the empty ones they meet across
    /// them, are one item where they make a whole rectangle (the 20:38 page of 2026-10-19: 34 "?" cells were the pieces of
    /// 11 rifles, silencers and scopes cut where the grid's line showed through them). Where they make none, a frame next
    /// to bright art stood out as faintly (the top of a junk box under a case: two items are never one shape), and the
    /// cells stay as the lines that show join them. A rectangle made so carries those pieces (<see cref="StashRect.Pieces"/>):
    /// two items of one width, one over the other, make a rectangle too (a keys case over a vest, 2026-10-19, 20:22), and a
    /// reader whose label names nothing of the rectangle's size reads the pieces instead.
    /// </summary>
    public static List<StashRect> Items(StashGrid grid, StashLines lines, bool[] hidden, bool[] empty, int pitch)
    {
        int cols = grid.Cols, area = grid.Area;
        // Each line between two cells not hidden: right of k, and below k.
        var right = new (double Share, double Height)?[area];
        var below = new (double Share, double Height)?[area];
        for (var k = 0; k < area; k++)
        {
            if (hidden[k])
                continue;
            int i = k % cols, j = k / cols;
            int cx = grid.X0 + i * pitch, cy = grid.Y0 + j * pitch;
            if (i + 1 < cols && !hidden[k + 1])
                right[k] = (lines.Vertical(cx + pitch, cy + 4, cy + pitch - 4), lines.VerticalHeight(cx + pitch, cy + 4, cy + pitch - 4));
            if (j + 1 < grid.Rows && !hidden[k + cols])
                below[k] = (lines.Horizontal(cy + pitch, cx + 4, cx + pitch - 4), lines.HorizontalHeight(cy + pitch, cx + 4, cx + pitch - 4));
        }
        var faint = Faint(right, below, empty, cols);

        // The cells the lines that show join (no line along most of the side).
        var shown = Enumerable.Range(0, area).ToArray();
        var whole = Enumerable.Range(0, area).ToArray();
        static int Find(int[] parent, int i) => parent[i] == i ? i : parent[i] = Find(parent, parent[i]);
        static void Join(int[] parent, int a, int b) => parent[Find(parent, a)] = Find(parent, b);
        bool Weak((double Share, double Height)? line) => line is { } l && l.Height < faint;
        for (var k = 0; k < area; k++)
        {
            if (hidden[k] || empty[k])
                continue;
            foreach (var (other, line) in new[] { (k + 1, right[k]), (k + cols, below[k]) })
            {
                if (line is not { } l || empty[other])
                    continue;
                if (l.Share < 0.4)
                {
                    Join(shown, k, other);
                    Join(whole, k, other);
                }
                else if (Weak(line))
                    Join(whole, k, other);
            }
        }

        // The same, across faint lines too, with the cells read as empty that meet such a group across them.
        var taken = new bool[area];
        if (faint > 0)
        {
            var changed = true;
            while (changed)
            {
                changed = false;
                for (var k = 0; k < area; k++)
                {
                    if (hidden[k] || !empty[k] || taken[k])
                        continue;
                    int i = k % cols, j = k / cols;
                    var owners = new HashSet<int>();
                    void Meet(int other, (double Share, double Height)? line)
                    {
                        if ((!empty[other] || taken[other]) && Weak(line))
                            owners.Add(Find(whole, other));
                    }
                    if (i > 0)
                        Meet(k - 1, right[k - 1]);
                    if (i + 1 < cols)
                        Meet(k + 1, right[k]);
                    if (j > 0)
                        Meet(k - cols, below[k - cols]);
                    if (k + cols < area)
                        Meet(k + cols, below[k]);
                    if (owners.Count == 0)
                        continue;
                    // Between two pieces of one item (a rifle's dark receiver between its stock and its barrel) it joins
                    // them; between two items it makes no rectangle, and they stay as the lines that show give them.
                    taken[k] = true;
                    foreach (var owner in owners)
                        Join(whole, k, owner);
                    changed = true;
                }
            }
        }

        static (int I0, int J0, int W, int H) Box(IEnumerable<int> cells, int cols)
        {
            var list = cells as IList<int> ?? cells.ToList();
            int i0 = list.Min(k => k % cols), j0 = list.Min(k => k / cols);
            return (i0, j0, list.Max(k => k % cols) - i0 + 1, list.Max(k => k / cols) - j0 + 1);
        }
        StashRect Rect(IReadOnlyCollection<int> cells)
        {
            var (i0, j0, w, h) = Box(cells, cols);
            var odd = cells.Count != w * h || w > 6 || h > 6;
            return new StashRect(i0, j0, w, h, odd) { Cells = odd ? cells.Select(k => (k % cols, k / cols)).ToList() : [] };
        }

        var items = new List<StashRect>();
        foreach (var group in Enumerable.Range(0, area).Where(k => !hidden[k] && (!empty[k] || taken[k])).GroupBy(k => Find(whole, k)))
        {
            var cells = group.ToList();
            var (_, _, w, h) = Box(cells, cols);
            // As the lines that show join them, the empty ones left out.
            var pieces = cells.Where(k => !empty[k]).GroupBy(k => Find(shown, k)).Select(g => Rect(g.ToList())).ToList();
            if (cells.Count == w * h && w <= 6 && h <= 6)
                items.Add(pieces.Count == 1 && cells.All(k => !empty[k]) ? pieces[0] : Rect(cells) with { Pieces = pieces });
            else
                items.AddRange(pieces);
        }
        return items.OrderBy(r => r.J0).ThenBy(r => r.I0).ToList();
    }

    // A line between two cells that stands out by less than this share of the grid's frames is the grid's own line seen
    // through an item's background: the frames stood out by 45 to 70 levels on the 20:38 page of 2026-10-19 and the lines
    // inside its wide items by 18 at most; on the GEAR page of 13:49 by 26 and 18 at most (a junk box's frame beside its
    // bright art 18 too, so the share is the frames', per grid, not a fixed level).
    private const double FaintShare = 0.45;

    // The fewest whole lines between items a grid must have for its frames' level to be taken (a compartment of one item
    // has none: there the shares alone decide, as before 2026-10-19).
    private const int LeastFrames = 4;

    /// <summary>How faintly a line between cells may stand out and still be no item's frame: <see cref="FaintShare"/> of the
    /// grid's frames (the median of the lines that show along their whole side, by a cell with something in it), or 0 where
    /// the grid has too few of them to tell.</summary>
    private static double Faint((double Share, double Height)?[] right, (double Share, double Height)?[] below, bool[] empty, int cols)
    {
        var heights = new List<double>();
        for (var k = 0; k < right.Length; k++)
        {
            if (right[k] is { Share: >= 0.9 } r && (!empty[k] || !empty[k + 1]))
                heights.Add(r.Height);
            if (below[k] is { Share: >= 0.9 } b && (!empty[k] || !empty[k + cols]))
                heights.Add(b.Height);
        }
        if (heights.Count < LeastFrames)
            return 0;
        heights.Sort();
        return FaintShare * heights[heights.Count / 2];
    }
}
