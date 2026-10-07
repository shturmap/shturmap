using SkiaSharp;
using Shturmap.Core;

namespace Shturmap.Map;

public static partial class MapRenderer
{
    // ---- the raid replay (owner, 2026-10-07; docs/DESIGN.md "Map drawing", *The raid replay*) ----

    /// <summary>How much the map recedes under the replay: the ground over it at this strength.</summary>
    public const float ReplayRecede = 0.45f;

    // The pen's dark collar, under the whole line (the ground at 59 %), and its width beyond the pen's widest.
    private const byte ReplayCollarAlpha = 150;
    private const float ReplayCollarWidth = 6.2f;
    private const float ReplayDot = 2.6f;

    /// <summary>
    /// The raid replay over the map: the map recedes, the pen runs from position to position up to the playhead in
    /// the colour and width of the raid's time (<see cref="ReplayInk"/>), each position a dot in its time's colour with
    /// its minute beside it, the newest one pinging; at the end the last one says when it was last seen. Drawn last,
    /// over everything.
    /// </summary>
    private static void DrawReplay(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        if (scene.Replay is not { } replay || replay.Fixes.Count == 0 || scene.ReplayOpacity <= 0)
            return;
        // Fading in and out, all of it as one.
        var fading = scene.ReplayOpacity < 0.999f;
        if (fading)
        {
            using var layer = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * scene.ReplayOpacity)) };
            canvas.SaveLayer(layer);
        }
        using (var recede = new SKPaint { Color = Background.WithAlpha((byte)Math.Round(255 * ReplayRecede)) })
            canvas.DrawRect(SKRect.Create(0, 0, camera.Viewport.Width, camera.Viewport.Height), recede);
        DrawReplayBody(canvas, camera, scene, replay, ui);
        if (fading)
            canvas.Restore();
    }

    private static void DrawReplayBody(SKCanvas canvas, Camera camera, MapScene scene, RaidReplay replay, float ui)
    {

        var now = scene.ReplayDone ? replay.Minutes : scene.ReplayMinute;
        var shown = replay.Fixes.Where(f => f.Minute <= now + 1e-9).ToList();
        var points = shown.Select(f => Screen(camera, scene, f.Position)).ToList();
        var legs = new List<(SKPoint From, SKPoint To, double TFrom, double TTo)>();
        for (var i = 1; i < points.Count; i++)
            legs.Add((points[i - 1], points[i], shown[i - 1].Minute / replay.Minutes, shown[i].Minute / replay.Minutes));
        // The pen grows toward the next position as the time runs.
        if (!scene.ReplayDone && points.Count > 0 && replay.Fixes.FirstOrDefault(f => f.Minute > now) is { } next)
        {
            var last = shown[^1];
            var k = (float)((now - last.Minute) / Math.Max(1e-9, next.Minute - last.Minute));
            var to = Screen(camera, scene, next.Position);
            legs.Add((points[^1], points[^1] + new SKPoint((to.X - points[^1].X) * k, (to.Y - points[^1].Y) * k),
                last.Minute / replay.Minutes, now / replay.Minutes));
        }
        DrawReplayPen(canvas, legs, ui);
        DrawReplayDots(canvas, points, shown.Select(f => f.Minute / replay.Minutes).ToList(), ui);

        // The newest position pings as a new position does, for as long as a ping lasts in the replay's time.
        if (!scene.ReplayDone && points.Count > 0)
        {
            var seconds = (now - shown[^1].Minute) * ReplayTiming.SecondsPerMinute(replay.Minutes);
            if (seconds < MapScene.PingLength.TotalSeconds)
                DrawPing(canvas, points[^1], seconds, scene.Pulse, ui, 3);
        }
        DrawReplayTags(canvas, camera, scene, replay, shown, points, ui);
    }

    // Each leg in short pieces, each in the colour and width of its moment, over one dark collar under the whole line.
    private static void DrawReplayPen(SKCanvas canvas, IReadOnlyList<(SKPoint From, SKPoint To, double TFrom, double TTo)> legs, float ui)
    {
        if (legs.Count == 0)
            return;
        using (var collar = new SKPaint
               {
                   Color = Background.WithAlpha(ReplayCollarAlpha), IsAntialias = true, Style = SKPaintStyle.Stroke,
                   StrokeWidth = ReplayCollarWidth * ui, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
               })
        using (var builder = new SKPathBuilder())
        {
            foreach (var (from, to, _, _) in legs)
            {
                builder.MoveTo(from);
                builder.LineTo(to);
            }
            using var path = builder.Detach();
            canvas.DrawPath(path, collar);
        }
        using var piece = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
        foreach (var (from, to, tFrom, tTo) in legs)
        {
            var length = SKPoint.Distance(from, to);
            var n = Math.Max(2, (int)Math.Ceiling(length / (5 * ui)));
            for (var i = 0; i < n; i++)
            {
                float f0 = (float)i / n, f1 = (float)(i + 1) / n;
                var t = tFrom + (tTo - tFrom) * (f0 + f1) / 2;
                piece.Color = ReplayInk.At(t);
                piece.StrokeWidth = ReplayInk.Width(t) * ui;
                canvas.DrawLine(from.X + (to.X - from.X) * f0, from.Y + (to.Y - from.Y) * f0,
                    from.X + (to.X - from.X) * f1, from.Y + (to.Y - from.Y) * f1, piece);
            }
        }
    }

    private static void DrawReplayDots(SKCanvas canvas, IReadOnlyList<SKPoint> points, IReadOnlyList<double> times, float ui)
    {
        using var collar = new SKPaint { Color = Background.WithAlpha(170), IsAntialias = true };
        using var dot = new SKPaint { IsAntialias = true };
        for (var i = 0; i < points.Count; i++)
        {
            canvas.DrawCircle(points[i], (ReplayDot + 1.5f) * ui, collar);
            dot.Color = ReplayInk.At(times[i]);
            canvas.DrawCircle(points[i], ReplayDot * ui, dot);
        }
    }

    // The minutes beside the positions, each where it covers no other tag, dot or the band at the foot; one with no
    // free place is left out (the timeline still has it). At the end the last position's tag says when it was last
    // seen, framed in sand as an old position's tag is, and it is placed first.
    private static void DrawReplayTags(SKCanvas canvas, Camera camera, MapScene scene, RaidReplay replay,
        IReadOnlyList<ReplayFix> shown, IReadOnlyList<SKPoint> points, float ui)
    {
        if (points.Count == 0)
            return;
        var foot = camera.Viewport.Height - scene.ReplayFoot * ui;
        var taken = points.Select(p => SKRect.Create(p.X - 5 * ui, p.Y - 5 * ui, 10 * ui, 10 * ui)).ToList();
        var order = Enumerable.Range(0, points.Count).ToList();
        if (scene.ReplayDone)
        {
            order.Remove(points.Count - 1);
            order.Insert(0, points.Count - 1);
        }
        using var plate = new SKPaint { Color = Background.WithAlpha(225), IsAntialias = true };
        using var ink = new SKPaint { Color = Ink, IsAntialias = true };
        using var sand = new SKPaint { Color = Player, IsAntialias = true };
        using var frame = new SKPaint { Color = Player, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * ui };
        foreach (var i in order)
        {
            var last = scene.ReplayDone && i == points.Count - 1;
            var text = last ? replay.LastSeenText : RaidReplay.MinuteText(shown[i].Minute);
            using var font = new SKFont(TypefaceBold, (last ? 12.5f : 10.5f) * ui);
            var w = font.MeasureText(text) + (last ? 12 : 8) * ui;
            var h = (last ? 20 : 15) * ui;
            var p = points[i];
            var gap = 8 * ui;
            SKRect? spot = null;
            foreach (var box in new[]
                     {
                         SKRect.Create(p.X + gap, p.Y - h / 2, w, h), SKRect.Create(p.X - gap - w, p.Y - h / 2, w, h),
                         SKRect.Create(p.X - w / 2, p.Y - gap - h, w, h), SKRect.Create(p.X - w / 2, p.Y + gap, w, h),
                         SKRect.Create(p.X + gap, p.Y - gap - h * 0.6f, w, h), SKRect.Create(p.X - gap - w, p.Y + gap * 0.4f, w, h),
                         SKRect.Create(p.X + gap, p.Y + gap * 0.4f, w, h), SKRect.Create(p.X - gap - w, p.Y - gap - h * 0.6f, w, h),
                     })
            {
                if (box.Left < 2 * ui || box.Top < 2 * ui || box.Right > camera.Viewport.Width - 2 * ui || box.Bottom > foot - 2 * ui
                    || taken.Any(t => t.IntersectsWith(box)))
                    continue;
                spot = box;
                break;
            }
            if (spot is not { } s)
                continue;
            taken.Add(s);
            canvas.DrawRect(s, plate);
            if (last)
                canvas.DrawRect(s, frame);
            canvas.DrawText(text, s.MidX, s.MidY + font.Size * 0.36f, SKTextAlign.Center, font, last ? sand : ink);
        }
    }
}
