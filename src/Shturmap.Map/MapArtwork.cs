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
/// is cached on disk in Skia's own format.
/// </summary>
public sealed class MapArtwork : IDisposable
{
    private readonly Dictionary<string, Lazy<SKPicture?>> _layers;

    private MapArtwork(SKPicture basePicture, Dictionary<string, Lazy<SKPicture?>> layers, SKRect viewBox)
    {
        Base = basePicture;
        _layers = layers;
        ViewBox = viewBox;
    }

    public SKPicture Base { get; }

    /// <summary>The SVG's viewBox, the coordinate system of the pictures.</summary>
    public SKRect ViewBox { get; }

    public SKPicture? Layer(string? svgLayer) => svgLayer is not null && _layers.TryGetValue(svgLayer, out var p) ? p.Value : null;

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
        return new MapArtwork(basePicture, layers, viewBox);
    }

    private static bool IsGroup(XElement e) => e.Name.LocalName == "g" && e.Attribute("id") is not null;

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
        Base.Dispose();
        foreach (var layer in _layers.Values.Where(l => l.IsValueCreated))
            layer.Value?.Dispose();
    }
}
