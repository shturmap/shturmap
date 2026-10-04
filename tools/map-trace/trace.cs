#:property ManagePackageVersionsCentrally=false
#:package OpenCvSharp4@4.10.0.20241108
#:package OpenCvSharp4.runtime.win@4.10.0.20241108
// Traces a floor plan out of a stitched tile render (tools\map-trace\stitch.cs) and writes it as SVG paths in map
// units, with a picture of the plan at the render's size and the numbers docs/MAP-TRACE.md's gates ask for.
//   dotnet run tools/map-trace/trace.cs -- <stitched.png> <out.svg> <plan.png> <tileSize> <zoom> <minTileX> <minTileY> <groupId> [threshold]
// tileSize is maps.json's (175 for The Lab, 256 where it gives none); zoom and the smallest tile x and y are what
// stitch.cs was given and printed; groupId names the floor's group. In tarkov.dev's renders everything that isn't
// floor is transparent or near black, walls included, so "drawn and not near black" is the floor.
using System.Globalization;
using System.Text;
using OpenCvSharp;

var inv = CultureInfo.InvariantCulture;
string input = args[0], svgOut = args[1], planOut = args[2];
double tileSize = double.Parse(args[3], inv);
int zoom = int.Parse(args[4]), minTileX = int.Parse(args[5]), minTileY = int.Parse(args[6]);
string groupId = args[7];
int threshold = args.Length > 8 ? int.Parse(args[8]) : 28;
const int TilePixels = 256;
// A dark thing on a floor smaller than this, and no longer than SpeckSide, is a prop or a stain: floor.
const int SpeckArea = 220, SpeckSide = 28;
// A floor patch smaller than this is a crumb: not floor.
const int CrumbArea = 300;
// An edge within this of an axis lies on it.
const double AxisTolerance = 8 * Math.PI / 180;
// One pixel of the stitched picture in map units (tarkov.dev's Leaflet pixels at zoom 0).
double unit = tileSize / (TilePixels * Math.Pow(2, zoom));

using var render = Cv2.ImRead(input, ImreadModes.Unchanged);
if (render.Channels() != 4)
    throw new InvalidOperationException("The stitched render must keep its alpha channel.");
Cv2.Split(render, out var channels);
using var brightest = new Mat();
Cv2.Max(channels[0], channels[1], brightest);
Cv2.Max(brightest, channels[2], brightest);
using var drawn = new Mat();
Cv2.Threshold(channels[3], drawn, 127, 255, ThresholdTypes.Binary);
using var bright = new Mat();
Cv2.Threshold(brightest, bright, threshold, 255, ThresholdTypes.Binary);
using var floor = new Mat();
Cv2.BitwiseAnd(drawn, bright, floor);
using var raw = floor.Clone();

// Specks become floor. A long thin dark thing is a wall and stays.
using (var notFloor = new Mat())
{
    Cv2.BitwiseNot(floor, notFloor);
    Relabel(notFloor, floor, 255, (w, h, area) => area < SpeckArea && Math.Max(w, h) < SpeckSide);
}
// Crumbs go.
Relabel(floor, floor, 0, (_, _, area) => area < CrumbArea);

Cv2.FindContours(floor, out Point[][] contours, out HierarchyIndex[] hierarchy, RetrievalModes.CComp, ContourApproximationModes.ApproxSimple);
var rings = new List<Ring>();
for (int i = 0; i < contours.Length; i++)
{
    var approx = Cv2.ApproxPolyDP(contours[i], 2.0, true);
    if (approx.Length >= 3)
        rings.Add(new Ring(i, hierarchy[i].Parent, approx.Select(p => new Point2d(p.X, p.Y)).ToArray()));
}

// Straighten: lines that nearly coincide anywhere on the map become one line, so walls come out parallel and of one
// thickness.
var xs = new List<(double At, double Weight)>();
var ys = new List<(double At, double Weight)>();
foreach (var ring in rings)
    for (int i = 0; i < ring.Points.Length; i++)
    {
        var a = ring.Points[i];
        var b = ring.Points[(i + 1) % ring.Points.Length];
        double length = Length(a, b);
        if (length < 1)
            continue;
        if (Upright(a, b)) xs.Add(((a.X + b.X) / 2, length));
        else if (Level(a, b)) ys.Add(((a.Y + b.Y) / 2, length));
    }
var xLines = Cluster(xs);
var yLines = Cluster(ys);
var straight = new List<Ring>();
double onAxis = 0, total = 0;
foreach (var ring in rings)
{
    var moved = (Point2d[])ring.Points.Clone();
    for (int i = 0; i < moved.Length; i++)
    {
        int j = (i + 1) % moved.Length;
        var a = ring.Points[i];
        var b = ring.Points[j];
        if (Upright(a, b))
            moved[i].X = moved[j].X = Nearest(xLines, (a.X + b.X) / 2);
        else if (Level(a, b))
            moved[i].Y = moved[j].Y = Nearest(yLines, (a.Y + b.Y) / 2);
    }
    // Points that repeat or lie on a straight run go.
    var clean = new List<Point2d>();
    for (int i = 0; i < moved.Length; i++)
    {
        var before = moved[(i + moved.Length - 1) % moved.Length];
        var at = moved[i];
        var after = moved[(i + 1) % moved.Length];
        if (Length(before, at) < 0.01)
            continue;
        double cross = (at.X - before.X) * (after.Y - at.Y) - (at.Y - before.Y) * (after.X - at.X);
        if (Math.Abs(cross) >= 0.01)
            clean.Add(at);
    }
    if (clean.Count < 3)
        continue;
    for (int i = 0; i < clean.Count; i++)
    {
        var a = clean[i];
        var b = clean[(i + 1) % clean.Count];
        total += Length(a, b);
        if (Math.Abs(a.X - b.X) < 0.01 || Math.Abs(a.Y - b.Y) < 0.01)
            onAxis += Length(a, b);
    }
    straight.Add(ring with { Points = clean.ToArray() });
}

// The plan as a picture at the render's size: to lay beside the render, and to measure against the mask.
var outlines = straight.Where(r => r.Parent < 0).OrderByDescending(Area).ToList();
var holes = straight.Where(r => r.Parent >= 0).ToList();
using var plan = new Mat(floor.Size(), MatType.CV_8UC1, Scalar.All(0));
Cv2.FillPoly(plan, outlines.Select(Pixels), Scalar.All(255));
Cv2.FillPoly(plan, holes.Select(Pixels), Scalar.All(0));
// An island inside a hole was painted over by the hole: paint it again, with its own holes.
foreach (var island in outlines.Skip(1))
{
    Cv2.FillPoly(plan, [Pixels(island)], Scalar.All(255));
    Cv2.FillPoly(plan, holes.Where(h => h.Parent == island.Index).Select(Pixels), Scalar.All(0));
}
using var differs = new Mat();
Cv2.BitwiseXor(plan, floor, differs);
// What still differs after a 2 px erosion is more than jitter along an edge.
using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
using var far = new Mat();
Cv2.Erode(differs, far, kernel);
double floorPixels = Cv2.CountNonZero(floor);
using var cleaned = new Mat();
Cv2.BitwiseXor(raw, floor, cleaned);
Console.WriteLine($"1 px = {unit:0.#####} map units");
Console.WriteLine($"rings: {straight.Count} ({outlines.Count} outlines, {holes.Count} holes), points: {straight.Sum(r => r.Points.Length)}");
Console.WriteLine($"GATE on an axis: {100 * onAxis / Math.Max(1, total):0.0} % of the outlines' length");
Console.WriteLine($"GATE differs from the mask: {100 * Cv2.CountNonZero(differs) / floorPixels:0.00} % of the floor");
Console.WriteLine($"GATE differs beyond 2 px of an edge: {100 * Cv2.CountNonZero(far) / floorPixels:0.000} % of the floor");
Console.WriteLine($"specks and crumbs: {100 * Cv2.CountNonZero(cleaned) / floorPixels:0.00} % of the floor");

using var picture = new Mat(floor.Size(), MatType.CV_8UC3, new Scalar(0x0B, 0x0C, 0x0B));
picture.SetTo(new Scalar(0x7F, 0x77, 0x70), plan);
Cv2.ImWrite(planOut, picture);

// The SVG: numbers are map units. The viewBox here is the stitched picture's rectangle; the app needs the map's
// artwork rectangle there instead (docs/MAP-TRACE.md, step 8), which changes no path.
double left = minTileX * TilePixels * unit, top = minTileY * TilePixels * unit;
var svg = new StringBuilder();
svg.Append(inv, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{left:0.###} {top:0.###} {floor.Width * unit:0.###} {floor.Height * unit:0.###}\">\n");
svg.Append("  <style id=\"style_common\">.floor { fill:#70777f }</style>\n");
svg.Append(inv, $"  <g id=\"{groupId}\">\n    <g id=\"Floor\" class=\"floor\">\n");
foreach (var outline in outlines)
{
    svg.Append("      <path fill-rule=\"evenodd\" d=\"");
    Write(outline);
    foreach (var hole in holes.Where(h => h.Parent == outline.Index))
        Write(hole);
    svg.Append("\"/>\n");
}
svg.Append("    </g>\n  </g>\n</svg>\n");
File.WriteAllText(svgOut, svg.ToString());
Console.WriteLine($"{svgOut}: {new FileInfo(svgOut).Length / 1024} KB");

void Write(Ring ring)
{
    for (int i = 0; i < ring.Points.Length; i++)
        svg.Append(inv, $"{(i == 0 ? "M" : "L")}{left + ring.Points[i].X * unit:0.###} {top + ring.Points[i].Y * unit:0.###}");
    svg.Append('Z');
}

// Sets every connected part of "of" that the rule picks to "value" in "target".
static void Relabel(Mat of, Mat target, byte value, Func<int, int, int, bool> picks)
{
    using var labels = new Mat();
    using var stats = new Mat();
    using var centroids = new Mat();
    int count = Cv2.ConnectedComponentsWithStats(of, labels, stats, centroids, PixelConnectivity.Connectivity4);
    var picked = new bool[count];
    for (int i = 1; i < count; i++)
        picked[i] = picks(stats.At<int>(i, 2), stats.At<int>(i, 3), stats.At<int>(i, 4));
    labels.GetArray(out int[] label);
    target.GetArray(out byte[] pixels);
    for (int i = 0; i < label.Length; i++)
        if (picked[label[i]])
            pixels[i] = value;
    target.SetArray(pixels);
}

static double Length(Point2d a, Point2d b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

static bool Upright(Point2d a, Point2d b) => Math.Abs(b.X - a.X) <= Math.Abs(b.Y - a.Y) * Math.Tan(AxisTolerance);

static bool Level(Point2d a, Point2d b) => Math.Abs(b.Y - a.Y) <= Math.Abs(b.X - a.X) * Math.Tan(AxisTolerance);

static double Area(Ring ring) => Math.Abs(Cv2.ContourArea(ring.Points.Select(p => new Point2f((float)p.X, (float)p.Y)).ToArray()));

static IEnumerable<Point> Pixels(Ring ring) => ring.Points.Select(p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y)));

// Lines within 1.5 px of each other become one, at their middle weighted by length.
static List<double> Cluster(List<(double At, double Weight)> values)
{
    var lines = new List<double>();
    double sum = 0, weight = 0, last = double.NaN;
    foreach (var (at, w) in values.OrderBy(v => v.At))
    {
        if (!double.IsNaN(last) && at - last > 1.5)
        {
            lines.Add(Math.Round(sum / weight));
            sum = 0;
            weight = 0;
        }
        sum += at * w;
        weight += w;
        last = at;
    }
    if (weight > 0)
        lines.Add(Math.Round(sum / weight));
    return lines;
}

static double Nearest(List<double> lines, double value)
{
    int i = lines.BinarySearch(value);
    if (i >= 0)
        return lines[i];
    i = ~i;
    if (i == 0)
        return lines[0];
    if (i == lines.Count)
        return lines[^1];
    return value - lines[i - 1] <= lines[i] - value ? lines[i - 1] : lines[i];
}

// A closed line of the floor: its number among the contours, its parent's (below 0 for an outline), its points.
record Ring(int Index, int Parent, Point2d[] Points);
