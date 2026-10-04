using System.Globalization;
using System.Xml.Linq;
using SkiaSharp;
using Shturmap.Core.Maps;
using Svg.Skia;

namespace Shturmap.Map;

/// <summary>
/// A map's SVG artwork as Skia pictures: the base picture (everything except the floor groups the map
/// definition lists as layers) and one picture per floor layer, mirroring tarkov.dev, which hides the layer
/// groups until a floor is selected. Parsing SVG is slow, so floors are parsed on first use and every picture
/// is cached on disk in Skia's own format. A view that draws frame after frame has them read ahead instead
/// (<see cref="ReadFloorsAsync"/>): then no paint waits for one.
/// </summary>
public sealed class MapArtwork : IDisposable
{
    private readonly Dictionary<string, Lazy<SKPicture?>> _layers;
    private readonly Lock _gate = new();
    private Task? _reading;
    private volatile bool _disposed;

    private MapArtwork(SKPicture basePicture, Dictionary<string, Lazy<SKPicture?>> layers, SKRect viewBox, bool showsMinefields,
        bool showsSniperZones)
    {
        Base = basePicture;
        _layers = layers;
        ViewBox = viewBox;
        ShowsMinefields = showsMinefields;
        ShowsSniperZones = showsSniperZones;
        Ground = Coverage(basePicture, viewBox);
    }

    public SKPicture Base { get; }

    /// <summary>
    /// Where the base picture draws anything (ground, water, buildings) as an alpha mask over <see cref="ViewBox"/>,
    /// drawn once when the artwork loads: the data's hazards are kept to it, so a minefield or sniper zone that runs past
    /// the drawn map doesn't hatch the empty space around it (owner, 2026-10-03). The SVGs have no background, so
    /// outside the drawn map is transparent. Null if it can't be drawn.
    /// </summary>
    public SKImage? Ground { get; }

    // The mask's longer side in pixels: about one pixel per screen pixel at the overview; zoomed in, its edge softens over
    // a few pixels, which a hatch's end doesn't mind.
    private const int GroundSize = 2048;

    /// <summary>
    /// The artwork draws minefields itself (a "mines" or "Minefield" group: Woods, Shoreline, Lighthouse, Streets,
    /// Terminal), so the data's minefield outlines aren't drawn over it (docs/DESIGN.md, "Landmarks").
    /// </summary>
    public bool ShowsMinefields { get; }

    /// <summary>
    /// The artwork draws the border snipers' kill zones itself: a "danger"-styled group named "Sniper" (Customs, Ground
    /// Zero, Streets) or "Danger" (Interchange), so the data's sniper zones aren't drawn over it (docs/DESIGN.md,
    /// "Landmarks"). Minefields are "danger"-styled too, but named "mines"; they don't count here.
    /// </summary>
    public bool ShowsSniperZones { get; }

    /// <summary>The SVG's viewBox, the coordinate system of the pictures.</summary>
    public SKRect ViewBox { get; }

    /// <summary>
    /// A floor's picture, or null for a floor the artwork doesn't have. It is read on first use, which takes a while
    /// (the SVG is parsed, or its saved picture read from disk), unless the floors are being read ahead
    /// (<see cref="ReadFloorsAsync"/>): then this never waits, gives null for a floor that isn't there yet, and
    /// <see cref="FloorRead"/> says when it is.
    /// </summary>
    public SKPicture? Layer(string? svgLayer)
    {
        if (svgLayer is null || !_layers.TryGetValue(svgLayer, out var layer))
            return null;
        if (layer.IsValueCreated)
            return layer.Value;
        return _reading is null ? layer.Value : null;
    }

    /// <summary>A floor was read ahead (<see cref="ReadFloorsAsync"/>); raised on a pool thread.</summary>
    public event Action? FloorRead;

    /// <summary>A floor couldn't be read ahead and is left out: its SVG group's id and why; raised on a pool thread.</summary>
    public event Action<string, Exception>? FloorFailed;

    /// <summary>Called before each floor is read ahead, with its SVG group's id (tests hold a read back with it).</summary>
    internal Action<string>? BeforeFloorRead { get; set; }

    /// <summary>
    /// Reads every floor in the background, one after the other, once: the task ends when all are there. A floor's
    /// picture used to be read by the first paint that showed it, on the drawing thread, which stood still for that
    /// long (the review of 2026-10-04, A37). From this call on <see cref="Layer"/> never waits. A floor that can't be
    /// drawn is left out, like one the artwork doesn't have.
    /// </summary>
    public Task ReadFloorsAsync()
    {
        lock (_gate)
            return _reading ??= Task.Run(ReadFloors);
    }

    private void ReadFloors()
    {
        foreach (var (id, layer) in _layers)
        {
            if (_disposed)
                return;
            try
            {
                BeforeFloorRead?.Invoke(id);
                var picture = layer.Value;
                // Closed while this floor was read: nothing keeps it.
                if (_disposed)
                    picture?.Dispose();
            }
            catch (Exception e)
            {
                // Whatever the SVG library makes of a group it can't draw: this floor is left out, the others are
                // still read, and the task ends well for whoever waits for it.
                FloorFailed?.Invoke(id, e);
                continue;
            }
            FloorRead?.Invoke();
        }
    }

    /// <param name="cacheFolder">Where parsed pictures are kept between runs; null to always parse.</param>
    public static MapArtwork Load(string svgPath, MapDefinition map, string? cacheFolder = null)
    {
        var doc = XDocument.Load(svgPath);
        var root = doc.Root ?? throw new FormatException("Empty SVG.");
        var viewBox = ParseViewBox(root);
        var groupIds = root.Elements().Where(IsGroup).Select(g => (string)g.Attribute("id")!).ToHashSet(StringComparer.Ordinal);
        var layerIds = map.Layers.Select(l => l.SvgLayer).OfType<string>().Where(groupIds.Contains).ToHashSet(StringComparer.Ordinal);

        var stamp = $"{Path.GetFileNameWithoutExtension(svgPath)}-{File.GetLastWriteTimeUtc(svgPath).Ticks}";
        string? CachePath(string part) => cacheFolder is null ? null : Path.Combine(cacheFolder, $"{stamp}-{part}.skp");

        var basePicture = Cached(CachePath("base"), () => Render(doc, id => !layerIds.Contains(id)));
        var layers = layerIds.ToDictionary(
            id => id,
            id => new Lazy<SKPicture?>(() => Cached(CachePath(id), () => Render(doc, g => g == id)), LazyThreadSafetyMode.ExecutionAndPublication),
            StringComparer.Ordinal);
        var showsMinefields = doc.Descendants().Any(e => ((string?)e.Attribute("id"))?.StartsWith("mine", StringComparison.OrdinalIgnoreCase) == true);
        var showsSniperZones = doc.Descendants().Any(e => (string?)e.Attribute("id") is { } id && (id.Equals("sniper", StringComparison.OrdinalIgnoreCase) || id.Equals("danger", StringComparison.OrdinalIgnoreCase)));
        return new MapArtwork(basePicture, layers, viewBox, showsMinefields, showsSniperZones);
    }

    private static bool IsGroup(XElement e) => e.Name.LocalName == "g" && e.Attribute("id") is not null;

    private static SKImage? Coverage(SKPicture picture, SKRect viewBox)
    {
        if (viewBox.Width <= 0 || viewBox.Height <= 0)
            return null;
        var scale = GroundSize / Math.Max(viewBox.Width, viewBox.Height);
        var info = new SKImageInfo((int)Math.Ceiling(viewBox.Width * scale), (int)Math.Ceiling(viewBox.Height * scale), SKColorType.Alpha8, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        if (surface is null)
            return null;
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.Scale(scale);
        surface.Canvas.Translate(-viewBox.Left, -viewBox.Top);
        surface.Canvas.DrawPicture(picture);
        return surface.Snapshot();
    }

    /// <summary>Whether the artwork draws anything at a point of its <see cref="ViewBox"/> (see <see cref="Ground"/>).</summary>
    public bool OnGround(SKPoint at)
    {
        if (Ground is null)
            return true;
        var x = (int)((at.X - ViewBox.Left) / ViewBox.Width * Ground.Width);
        var y = (int)((at.Y - ViewBox.Top) / ViewBox.Height * Ground.Height);
        if (x < 0 || y < 0 || x >= Ground.Width || y >= Ground.Height)
            return false;
        using var pixels = Ground.PeekPixels();
        return pixels is null || pixels.GetPixelColor(x, y).Alpha > 127;
    }

    private static SKPicture Cached(string? path, Func<SKPicture> render)
    {
        if (path is not null && File.Exists(path))
        {
            try
            {
                using var stream = File.OpenRead(path);
                if (SKPicture.Deserialize(stream) is { } cached)
                    return cached;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Another Shturmap is replacing it this moment: draw it here instead.
            }
        }
        var picture = render();
        if (path is not null)
        {
            // The picture cache is shared by every Shturmap on the PC: written to a temporary file of its own and
            // moved over in one go, so no other process reads half a file (Shturmap.Data.Http.CachedHttp). A copy that
            // can't be saved now is drawn again next time.
            var temp = Shturmap.Data.Http.CachedHttp.TempFor(path);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using (var data = picture.Serialize())
                using (var file = File.Create(temp))
                    data.SaveTo(file);
                Shturmap.Data.Http.CachedHttp.Replace(temp, path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
            finally
            {
                Shturmap.Data.Http.CachedHttp.TryDelete(temp);
            }
        }
        return picture;
    }

    // Draws a copy of the SVG that keeps only the top-level groups the filter wants (plus styles and defs).
    private static SKPicture Render(XDocument source, Func<string, bool> keepGroup)
    {
        var doc = new XDocument(source);
        foreach (var group in doc.Root!.Elements().Where(IsGroup).ToList())
        {
            if (!keepGroup((string)group.Attribute("id")!))
                group.Remove();
        }
        using var stream = new MemoryStream();
        doc.Save(stream);
        stream.Position = 0;
        using var svg = new SKSvg();
        var picture = svg.Load(stream) ?? throw new FormatException("SVG could not be drawn.");
        // Re-record so the picture outlives the SKSvg that produced it.
        using var recorder = new SKPictureRecorder();
        recorder.BeginRecording(picture.CullRect).DrawPicture(picture);
        return recorder.EndRecording();
    }

    private static SKRect ParseViewBox(XElement root)
    {
        var parts = ((string?)root.Attribute("viewBox") ?? "")
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => float.Parse(p, CultureInfo.InvariantCulture))
            .ToArray();
        if (parts.Length == 4)
            return SKRect.Create(parts[0], parts[1], parts[2], parts[3]);
        var w = float.Parse(((string?)root.Attribute("width") ?? "1000").TrimEnd('p', 'x'), CultureInfo.InvariantCulture);
        var h = float.Parse(((string?)root.Attribute("height") ?? "1000").TrimEnd('p', 'x'), CultureInfo.InvariantCulture);
        return SKRect.Create(0, 0, w, h);
    }

    public void Dispose()
    {
        _disposed = true;
        Base.Dispose();
        Ground?.Dispose();
        foreach (var layer in _layers.Values.Where(l => l.IsValueCreated))
            layer.Value?.Dispose();
    }
}
