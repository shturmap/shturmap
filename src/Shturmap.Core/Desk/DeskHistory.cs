using System.Text.Json;

namespace Shturmap.Core.Desk;

// An item's flea price history for the desk's card (owner, 2026-10-19, from a panel: C1; docs/DESIGN.md §3, "The stash
// desk's prices come per reading", and §4, *The card*): json.tarkov.dev's "/{mode}/prices/{id}", a point a day for the
// item's whole history and every 2 h over the last 30 days, each with the average and the lowest offer (tarkov.dev keeps
// no maximum). Fetched when a card first shows the item, never on a timer; the made-up desk makes its own.

/// <summary>One point of the history: when, the average of the offers, the lowest offer, and how many offers (recent
/// points only).</summary>
public readonly record struct DeskPricePoint(DateTimeOffset At, double Avg, double Min, int? Offers = null);

/// <summary>The 30 days' high and low in words ("HIGH 1.05 M · LOW 0.82 M, 13 DAYS AGO"): the highest average, the lowest
/// offer and how many days before the last point it was.</summary>
public readonly record struct DeskHistoryRange(double High, double Low, int LowDaysAgo);

/// <summary>An item's price history, its points in time order.</summary>
public sealed class DeskHistory
{
    /// <summary>The band's span: 30 days back from the last point.</summary>
    public static readonly TimeSpan Band = TimeSpan.FromDays(30);

    public DeskHistory(IEnumerable<DeskPricePoint> points)
    {
        Points = points.Where(p => p.Avg > 0 || p.Min > 0).OrderBy(p => p.At).ToList();
    }

    public IReadOnlyList<DeskPricePoint> Points { get; }

    /// <summary>The last point's time, or null without points.</summary>
    public DateTimeOffset? Last => Points.Count > 0 ? Points[^1].At : null;

    /// <summary>
    /// The history in tarkov.dev's file: <c>{"data":[{"priceMin":…,"price":…,"offerCount":…,"timestamp":ms}, …]}</c> (the
    /// points' shape read 2026-10-19 and 2026-10-19; docs/UPDATES.md), or a bare array of such points. A point without a
    /// time or a price is left out; null when the text isn't such a file.
    /// </summary>
    public static DeskHistory? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var points = root.ValueKind == JsonValueKind.Array ? root
                : root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array ? data
                : default;
            if (points.ValueKind != JsonValueKind.Array)
                return null;
            var list = new List<DeskPricePoint>();
            foreach (var point in points.EnumerateArray())
            {
                if (point.ValueKind != JsonValueKind.Object || Number(point, "timestamp") is not { } ms)
                    continue;
                var avg = Number(point, "price") ?? 0;
                var min = Number(point, "priceMin") ?? avg;
                if (avg <= 0 && min <= 0)
                    continue;
                list.Add(new DeskPricePoint(DateTimeOffset.FromUnixTimeMilliseconds((long)ms), avg > 0 ? avg : min, min > 0 ? min : avg,
                    Number(point, "offerCount") is { } offers ? (int)offers : null));
            }
            return new DeskHistory(list);
        }
        catch (Exception e) when (e is JsonException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return null;
        }
    }

    private static double? Number(JsonElement point, string name) =>
        point.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d) ? d : null;

    /// <summary>The points of the last <paramref name="span"/> before the last point, the band's (30 days: a point every
    /// 2 h).</summary>
    public IReadOnlyList<DeskPricePoint> Recent(TimeSpan span) =>
        Last is { } last ? Points.Where(p => p.At > last - span).ToList() : [];

    /// <summary>
    /// The 7-day change in percent: the average of the last day's averages against the average of the day a week before
    /// it (a day of points either side, so one point's spike doesn't make the change); null where either day has none.
    /// </summary>
    public double? Change(TimeSpan back)
    {
        if (Last is not { } last)
            return null;
        var day = TimeSpan.FromDays(1);
        var now = Points.Where(p => p.At > last - day).ToList();
        var then = Points.Where(p => p.At > last - back - day && p.At <= last - back).ToList();
        if (now.Count == 0 || then.Count == 0)
            return null;
        var before = then.Average(p => p.Avg);
        return before > 0 ? (now.Average(p => p.Avg) / before - 1) * 100 : null;
    }

    /// <summary>The 7-day change (*The card*, "↑ +1.7 % · 7 D").</summary>
    public double? Change7d => Change(TimeSpan.FromDays(7));

    /// <summary>The 30 days' highest average and lowest offer, with how many whole days before the last point the low was;
    /// null without points.</summary>
    public DeskHistoryRange? Range()
    {
        var band = Recent(Band);
        if (band.Count == 0)
            return null;
        var low = band.MinBy(p => p.Min);
        return new DeskHistoryRange(band.Max(p => p.Avg), low.Min, (int)Math.Floor((Last!.Value - low.At).TotalDays));
    }
}

/// <summary>
/// The day's range bar (*The card*, 24 H): from the day's low to its high, with the average and the last low marked. A
/// high far past the rest (more than twice the average and the last low: one offer, likely an outlier, as the LEDX's 9 999 999
/// on 2026-10-19) isn't drawn: the bar ends a tenth past the average or the last low, and a line says the high.
/// </summary>
public sealed record DeskDayRange(double Low, double High, double BarHigh, double? Avg, double? LastLow)
{
    /// <summary>The high is past the bar: an outlier said in a line, not drawn.</summary>
    public bool Outlier => High > BarHigh;

    /// <summary>The bar for an item's day, or null where the item file has no day's low and high.</summary>
    public static DeskDayRange? Of(DeskItemFacts facts)
    {
        if (facts.Low24h is not > 0 || facts.High24h is not > 0)
            return null;
        var low = facts.Low24h.Value;
        var high = Math.Max(low, facts.High24h.Value);
        var middle = Math.Max(facts.Avg24h ?? 0, facts.LastLow ?? 0);
        var bar = middle > 0 && high > 2 * middle ? Math.Max(low, middle * 1.1) : high;
        return new DeskDayRange(low, high, bar, facts.Avg24h is > 0 ? facts.Avg24h : null, facts.LastLow is > 0 ? facts.LastLow : null);
    }

    /// <summary>Where a price stands on the bar, 0 at its low to 1 at its end.</summary>
    public double At(double price) => BarHigh > Low ? Math.Clamp((price - Low) / (BarHigh - Low), 0, 1) : 0.5;
}
