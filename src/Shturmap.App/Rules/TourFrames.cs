namespace Shturmap.App.Rules;

/// <summary>
/// The tour's cut-outs: the parts of the window a chapter frames, and how they move from one chapter to the next
/// (docs/DESIGN.md §4, *The tour*). Plain rectangles in the window's coordinates.
/// </summary>
public static class TourFrames
{
    /// <summary>A cut-out.</summary>
    public readonly record struct Box(double X, double Y, double Width, double Height)
    {
        public double Right => X + Width;
        public double Bottom => Y + Height;
        public double Area => Width * Height;
        public bool IsEmpty => Width <= 0 || Height <= 0;

        public bool Touches(Box other) => X <= other.Right && other.X <= Right && Y <= other.Bottom && other.Y <= Bottom;

        public static Box Lerp(Box a, Box b, double t) =>
            new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, Math.Max(0, a.Width + (b.Width - a.Width) * t), Math.Max(0, a.Height + (b.Height - a.Height) * t));

        internal double Distance(Box other)
        {
            var dx = X + Width / 2 - (other.X + other.Width / 2);
            var dy = Y + Height / 2 - (other.Y + other.Height / 2);
            return dx * dx + dy * dy;
        }
    }

    /// <summary>
    /// Parts that touch or overlap are one cut-out, their bounding box (the three buttons at the top right): their
    /// corner marks would cross.
    /// </summary>
    public static List<Box> Merged(IEnumerable<Box> boxes)
    {
        var merged = boxes.Where(b => !b.IsEmpty).ToList();
        for (var again = true; again;)
        {
            again = false;
            for (var i = 0; i < merged.Count && !again; i++)
            {
                for (var j = i + 1; j < merged.Count && !again; j++)
                {
                    if (!merged[i].Touches(merged[j]))
                        continue;
                    Box a = merged[i], b = merged[j];
                    var x = Math.Min(a.X, b.X);
                    var y = Math.Min(a.Y, b.Y);
                    merged[i] = new Box(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
                    merged.RemoveAt(j);
                    again = true;
                }
            }
        }
        return merged;
    }

    /// <summary>
    /// Which cut-out becomes which when a chapter changes its parts (owner, 2026-10-09: "sometimes they transition into
    /// nothingness, like from section 3 to section 4"). Each new part, the largest first, takes the nearest drawn one;
    /// a drawn one left over glides into the new part nearest it, and a new part left over grows out of the drawn one
    /// nearest it. Nothing shrinks into a point while there is a part to go to. Both lists must hold something: with
    /// nothing drawn or nothing to draw, the parts fade instead.
    /// </summary>
    public static List<(Box From, Box To)> Pairs(IReadOnlyList<Box> from, IReadOnlyList<Box> to)
    {
        if (from.Count == 0 || to.Count == 0)
            throw new ArgumentException("Both the drawn parts and the new ones are needed; with none on a side they fade.");
        var pairs = new List<(Box, Box)>();
        var left = from.ToList();
        foreach (var target in to.OrderByDescending(b => b.Area))
        {
            var source = (left.Count > 0 ? left : from).MinBy(f => f.Distance(target));
            left.Remove(source);
            pairs.Add((source, target));
        }
        foreach (var source in left)
            pairs.Add((source, to.MinBy(t => t.Distance(source))));
        return pairs;
    }

    /// <summary>
    /// The union of the cut-outs as boxes that don't overlap, for the dim to leave out. Two cut-outs that cross while
    /// they glide would otherwise be dimmed again where they overlap (the dim cuts its holes even-odd). Cut into strips
    /// at every left and right edge, the spans in each strip joined, and a piece carried on into the next strip while
    /// its span stays the same.
    /// </summary>
    public static List<Box> Disjoint(IEnumerable<Box> boxes)
    {
        const double same = 1e-6;
        var live = boxes.Where(b => !b.IsEmpty).ToList();
        var xs = live.SelectMany(b => new[] { b.X, b.Right }).Distinct().Order().ToList();
        var done = new List<Box>();
        var open = new List<Box>();
        for (var i = 0; i + 1 < xs.Count; i++)
        {
            double x0 = xs[i], x1 = xs[i + 1];
            if (x1 - x0 < same)
                continue;
            var spans = new List<(double Top, double Bottom)>();
            foreach (var b in live.Where(b => b.X <= x0 + same && b.Right >= x1 - same).OrderBy(b => b.Y))
            {
                if (spans.Count > 0 && b.Y <= spans[^1].Bottom)
                    spans[^1] = (spans[^1].Top, Math.Max(spans[^1].Bottom, b.Bottom));
                else
                    spans.Add((b.Y, b.Bottom));
            }
            var next = new List<Box>();
            foreach (var (top, bottom) in spans)
            {
                var j = open.FindIndex(p => Math.Abs(p.Y - top) < same && Math.Abs(p.Bottom - bottom) < same && Math.Abs(p.Right - x0) < same);
                if (j >= 0)
                {
                    var p = open[j];
                    open.RemoveAt(j);
                    next.Add(new Box(p.X, top, x1 - p.X, bottom - top));
                }
                else
                {
                    next.Add(new Box(x0, top, x1 - x0, bottom - top));
                }
            }
            done.AddRange(open);
            open = next;
        }
        done.AddRange(open);
        return done;
    }
}
