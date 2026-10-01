using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spotter.Core.Maps;

/// <summary>Reads tarkov.dev's src/data/maps.json. Only "interactive" entries are kept.</summary>
public static class MapDefinitionReader
{
    public static IReadOnlyList<MapDefinition> Read(string json)
    {
        var root = JsonNode.Parse(json) as JsonArray ?? throw new FormatException("maps.json is not an array.");
        var result = new List<MapDefinition>();
        foreach (var group in root.OfType<JsonObject>())
        {
            if (group["maps"] is not JsonArray entries)
                continue;
            foreach (var entry in entries.OfType<JsonObject>())
            {
                if (Str(entry, "projection") != "interactive" || Str(entry, "key") is not { } key)
                    continue;
                if (Box(entry["bounds"]) is not { } bounds || Numbers(entry["transform"]) is not { Length: 4 } transform)
                    continue;

                result.Add(new MapDefinition
                {
                    Key = key,
                    AltKeys = Strings(entry["altMaps"]),
                    Transform = transform,
                    CoordinateRotation = Num(entry["coordinateRotation"]) ?? 0,
                    Bounds = bounds,
                    SvgBounds = Box(entry["svgBounds"]),
                    SvgPath = Str(entry, "svgPath"),
                    SvgLayer = Str(entry, "svgLayer"),
                    TilePath = Str(entry, "tilePath"),
                    TileSize = (int)(Num(entry["tileSize"]) ?? 256),
                    MinZoom = Num(entry["minZoom"]) ?? 0,
                    MaxZoom = Num(entry["maxZoom"]) ?? 5,
                    BaseHeight = Range(entry["heightRange"]) ?? HeightRange.Unbounded,
                    Layers = Layers(entry["layers"]),
                    Labels = Labels(entry["labels"]),
                    Author = Str(entry, "author"),
                    AuthorLink = Str(entry, "authorLink"),
                });
            }
        }
        return result;
    }

    private static List<MapLayer> Layers(JsonNode? node)
    {
        var layers = new List<MapLayer>();
        if (node is not JsonArray array)
            return layers;
        foreach (var layer in array.OfType<JsonObject>())
        {
            var extents = new List<LayerExtent>();
            if (layer["extents"] is JsonArray ex)
            {
                foreach (var e in ex.OfType<JsonObject>())
                {
                    var boxes = new List<WorldBox>();
                    if (e["bounds"] is JsonArray bs)
                        boxes.AddRange(bs.Select(Box).OfType<WorldBox>());
                    extents.Add(new LayerExtent(Range(e["height"]) ?? HeightRange.Unbounded, boxes));
                }
            }
            layers.Add(new MapLayer(
                Str(layer, "name") ?? "Layer",
                Str(layer, "svgLayer"),
                Str(layer, "tilePath"),
                layer["show"]?.GetValueKind() == JsonValueKind.True,
                extents));
        }
        return layers;
    }

    private static List<MapLabel> Labels(JsonNode? node)
    {
        var labels = new List<MapLabel>();
        if (node is not JsonArray array)
            return labels;
        foreach (var label in array.OfType<JsonObject>())
        {
            if (Numbers(label["position"]) is not { Length: >= 2 } pos || Str(label, "text") is not { } text)
                continue;
            labels.Add(new MapLabel(pos[0], pos[1], text, Num(label["rotation"]) ?? 0, Num(label["size"]) ?? 60));
        }
        return labels;
    }

    // [[x1, z1], [x2, z2]] or, inside layer extents, [[x1, z1], [x2, z2], "label"]
    private static WorldBox? Box(JsonNode? node)
    {
        if (node is not JsonArray { Count: >= 2 } a || Numbers(a[0]) is not { Length: 2 } p1 || Numbers(a[1]) is not { Length: 2 } p2)
            return null;
        return new WorldBox(p1[0], p1[1], p2[0], p2[1]);
    }

    private static HeightRange? Range(JsonNode? node) =>
        Numbers(node) is { Length: 2 } r ? new HeightRange(r[0], r[1]) : null;

    private static double[]? Numbers(JsonNode? node)
    {
        if (node is not JsonArray array)
            return null;
        var values = new double[array.Count];
        for (var i = 0; i < array.Count; i++)
        {
            if (Num(array[i]) is not { } v)
                return null;
            values[i] = v;
        }
        return values;
    }

    private static double? Num(JsonNode? node) => node is JsonValue v
        ? v.GetValueKind() switch
        {
            JsonValueKind.Number => v.GetValue<double>(),
            JsonValueKind.String when double.TryParse(v.GetValue<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        }
        : null;

    private static string? Str(JsonObject obj, string name) =>
        obj[name] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    private static List<string> Strings(JsonNode? node) =>
        node is JsonArray a
            ? a.OfType<JsonValue>().Where(v => v.GetValueKind() == JsonValueKind.String).Select(v => v.GetValue<string>()).ToList()
            : [];
}
