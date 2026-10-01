using SkiaSharp;
using Spotter.Core;
using Spotter.Core.Maps;

namespace Spotter.Map;

/// <summary>Draws a <see cref="MapScene"/> through a <see cref="Camera"/>. Markers keep a fixed screen size.</summary>
public static class MapRenderer
{
    private static readonly SKColor Background = SKColor.Parse("#0e1413");
    private static readonly SKColor Amber = SKColor.Parse("#e49a3c");
    private static readonly SKColor Green = SKColor.Parse("#6cc38e");
    private static readonly SKColor Teal = SKColor.Parse("#74b8b1");
    private static readonly SKColor Lime = SKColor.Parse("#c9d46a");
    private static readonly SKColor Violet = SKColor.Parse("#b598e8");
    private static readonly SKColor Player = SKColor.Parse("#f2d79c");
    private static readonly SKColor Ink = SKColor.Parse("#e1e6de");

    private static readonly SKTypeface Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal) ?? SKTypeface.Default;
    private static readonly SKTypeface TypefaceBold = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold) ?? SKTypeface.Default;

    public static void Render(SKCanvas canvas, Camera camera, MapScene scene, float uiScale = 1)
    {
        canvas.Clear(Background);
        DrawArtwork(canvas, camera, scene);

        var placed = new List<(SKRect Box, string? Text)>();
        if (scene.ShowLabels)
            DrawMapLabels(canvas, camera, scene, uiScale, placed);
        foreach (var zone in scene.Zones)
            DrawZone(canvas, camera, scene, zone);
        DrawTrail(canvas, camera, scene, uiScale);
        DrawGuide(canvas, camera, scene, uiScale);
        foreach (var marker in scene.Markers.OrderBy(m => IsSelected(scene, m) ? 1 : 0))
            DrawMarker(canvas, camera, scene, marker, uiScale, placed);
        DrawPlayer(canvas, camera, scene, uiScale);
    }

    private static bool IsSelected(MapScene scene, MapMarker m) =>
        scene.Selected is not null && (m.Id == scene.Selected || m.Group == scene.Selected);

    private static void DrawArtwork(SKCanvas canvas, Camera camera, MapScene scene)
    {
        canvas.Save();
        canvas.SetMatrix(camera.Matrix);
        canvas.Translate((float)scene.Placement.OffsetX, (float)scene.Placement.OffsetY);
        canvas.Scale((float)scene.Placement.Scale);
        canvas.DrawPicture(scene.Artwork.Base);
        if (scene.Artwork.Layer(scene.Floor?.SvgLayer) is { } floor)
        {
            using var dim = new SKPaint { Color = Background.WithAlpha(150) };
            canvas.DrawRect(scene.Artwork.ViewBox, dim);
            canvas.DrawPicture(floor);
        }
        canvas.Restore();
    }

    private static SKPoint Screen(Camera camera, MapScene scene, WorldPoint p) => camera.ToScreen(scene.Projection.ToMap(p));

    private static void DrawMapLabels(SKCanvas canvas, Camera camera, MapScene scene, float ui, List<(SKRect Box, string? Text)> placed)
    {
        using var font = new SKFont(Typeface, 11 * ui);
        using var paint = new SKPaint { Color = Ink.WithAlpha(150), IsAntialias = true };
        foreach (var label in scene.Definition.Labels)
        {
            var at = camera.ToScreen(scene.Projection.ToMap(label.X, label.Z));
            var width = font.MeasureText(label.Text);
            var box = SKRect.Create(at.X - width / 2, at.Y - font.Size, width, font.Size * 1.3f);
            if (placed.Any(p => p.Box.IntersectsWith(box)))
                continue;
            canvas.Save();
            canvas.RotateDegrees((float)label.Rotation, at.X, at.Y);
            canvas.DrawText(label.Text.ToUpperInvariant(), at.X, at.Y, SKTextAlign.Center, font, paint);
            canvas.Restore();
            placed.Add((box, null));
        }
    }

    private static void DrawZone(SKCanvas canvas, Camera camera, MapScene scene, MapZone zone)
    {
        if (zone.Outline.Count < 3)
            return;
        using var path = Polygon(zone.Outline.Select(p => Screen(camera, scene, p)).ToArray());
        var color = ColorOf(zone.Kind);
        var selected = scene.Selected is not null && zone.Group == scene.Selected;
        using var fill = new SKPaint { Color = color.WithAlpha((byte)(selected ? 70 : 35)), IsAntialias = true };
        using var stroke = new SKPaint { Color = color.WithAlpha((byte)(selected ? 230 : 140)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = selected ? 2 : 1.2f };
        canvas.DrawPath(path, fill);
        canvas.DrawPath(path, stroke);
    }

    private static void DrawTrail(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        if (scene.Trail.Count == 0)
            return;
        var points = scene.Trail.Select(p => Screen(camera, scene, p)).ToList();
        if (scene.Player is { } player)
            points.Add(Screen(camera, scene, player.Position));
        using var line = new SKPaint
        {
            Color = Teal.WithAlpha(170), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * ui,
            PathEffect = SKPathEffect.CreateDash([4 * ui, 4 * ui], 0),
        };
        using var dot = new SKPaint { Color = Teal.WithAlpha(200), IsAntialias = true };
        for (var i = 1; i < points.Count; i++)
            canvas.DrawLine(points[i - 1], points[i], line);
        foreach (var p in points.Take(scene.Trail.Count))
            canvas.DrawCircle(p, 3 * ui, dot);
    }

    // A dashed line from the player to the selected objective.
    private static void DrawGuide(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        if (scene.Player is not { } player || scene.Selected is null)
            return;
        var targets = scene.Markers.Where(m => IsSelected(scene, m) && m.Kind != MarkerKind.ObjectiveDone).ToList();
        if (targets.Count == 0)
            return;
        var nearest = targets.MinBy(m => player.Position.HorizontalDistanceTo(m.Position))!;
        using var guide = new SKPaint
        {
            Color = Amber.WithAlpha(200), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * ui,
            PathEffect = SKPathEffect.CreateDash([6 * ui, 5 * ui], 0),
        };
        canvas.DrawLine(Screen(camera, scene, player.Position), Screen(camera, scene, nearest.Position), guide);
    }

    private static void DrawMarker(SKCanvas canvas, Camera camera, MapScene scene, MapMarker marker, float ui, List<(SKRect Box, string? Text)> placed)
    {
        var at = Screen(camera, scene, marker.Position);
        var selected = IsSelected(scene, marker);
        var color = ColorOf(marker.Kind);
        var r = (selected ? 8f : 6f) * ui;
        using var fill = new SKPaint { Color = color, IsAntialias = true };
        using var outline = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * ui };

        switch (marker.Kind)
        {
            case MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared:
                using (var tri = Polygon(new(at.X, at.Y - r * 1.2f), new(at.X + r * 1.1f, at.Y + r * 0.8f), new(at.X - r * 1.1f, at.Y + r * 0.8f)))
                {
                    canvas.DrawPath(tri, fill);
                    canvas.DrawPath(tri, outline);
                }
                break;
            case MarkerKind.Transit:
                using (var diamond = Polygon(new(at.X, at.Y - r), new(at.X + r, at.Y), new(at.X, at.Y + r), new(at.X - r, at.Y)))
                {
                    canvas.DrawPath(diamond, fill);
                    canvas.DrawPath(diamond, outline);
                }
                break;
            case MarkerKind.PossibleLocation:
                using (var ring = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f * ui })
                {
                    canvas.DrawCircle(at, r, ring);
                    canvas.DrawCircle(at, r * 0.35f, fill);
                }
                break;
            case MarkerKind.ObjectiveDone:
                fill.Color = color.WithAlpha(150);
                canvas.DrawCircle(at, r * 0.8f, fill);
                break;
            default:
                canvas.DrawCircle(at, r, fill);
                canvas.DrawCircle(at, r, outline);
                break;
        }

        if (!scene.ShowLabels && !selected)
            return;
        using var font = new SKFont(selected ? TypefaceBold : Typeface, (selected ? 13 : 11.5f) * ui);
        var width = font.MeasureText(marker.Label);
        var box = SKRect.Create(at.X + r + 4 * ui, at.Y - font.Size * 0.75f, width, font.Size * 1.3f);
        // Unselected labels give way to anything already placed; a selected label only to its own copies nearby.
        var blocking = selected ? placed.Where(p => p.Text == marker.Label) : placed;
        if (blocking.Any(p => p.Box.IntersectsWith(box)))
            return;
        using var shadow = new SKPaint { Color = Background.WithAlpha(220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ui };
        using var text = new SKPaint { Color = selected ? color : Ink, IsAntialias = true };
        canvas.DrawText(marker.Label, box.Left, at.Y + font.Size * 0.35f, SKTextAlign.Left, font, shadow);
        canvas.DrawText(marker.Label, box.Left, at.Y + font.Size * 0.35f, SKTextAlign.Left, font, text);
        placed.Add((box, marker.Label));
    }

    private static void DrawPlayer(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        if (scene.Player is not { } player)
            return;
        var at = Screen(camera, scene, player.Position);
        if (player.YawDegrees is { } yaw)
        {
            var heading = (float)scene.Projection.ScreenHeadingDegrees(player.Position, yaw);
            canvas.Save();
            canvas.RotateDegrees(heading, at.X, at.Y);
            using var cone = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(at, 70 * ui, [Player.WithAlpha(110), Player.WithAlpha(0)], SKShaderTileMode.Clamp),
            };
            using var wedgeBuilder = new SKPathBuilder();
            wedgeBuilder.MoveTo(at);
            wedgeBuilder.ArcTo(SKRect.Create(at.X - 70 * ui, at.Y - 70 * ui, 140 * ui, 140 * ui), -90 - 28, 56, false);
            wedgeBuilder.Close();
            using var wedge = wedgeBuilder.Detach();
            canvas.DrawPath(wedge, cone);
            using var arrow = Polygon(new(at.X, at.Y - 17 * ui), new(at.X - 6 * ui, at.Y - 8 * ui), new(at.X + 6 * ui, at.Y - 8 * ui));
            using var arrowPaint = new SKPaint { Color = Player, IsAntialias = true };
            canvas.DrawPath(arrow, arrowPaint);
            canvas.Restore();
        }
        using var halo = new SKPaint { Color = Player.WithAlpha(45), IsAntialias = true };
        using var body = new SKPaint { Color = Player, IsAntialias = true };
        using var edge = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f * ui };
        canvas.DrawCircle(at, 16 * ui, halo);
        canvas.DrawCircle(at, 6.5f * ui, body);
        canvas.DrawCircle(at, 6.5f * ui, edge);
    }

    private static SKPath Polygon(params SKPoint[] points)
    {
        using var builder = new SKPathBuilder();
        builder.AddPoly(points, true);
        return builder.Detach();
    }

    private static SKColor ColorOf(MarkerKind kind) => kind switch
    {
        MarkerKind.Objective or MarkerKind.PossibleLocation => Amber,
        MarkerKind.ObjectiveDone => Green,
        MarkerKind.ExtractPmc => Green,
        MarkerKind.ExtractScav => Teal,
        MarkerKind.ExtractShared => Lime,
        MarkerKind.Transit => Violet,
        _ => Ink,
    };
}
