using System.Globalization;
using System.Text.RegularExpressions;

namespace Spotter.Core.Screenshots;

/// <summary>What the game encodes in a screenshot's file name.</summary>
/// <param name="TakenAt">Local wall-clock time to the minute, as written in the name.</param>
/// <param name="Counter">The "(n)" suffix: shots taken in the same minute.</param>
/// <param name="Position">World position; only present for screenshots taken in a raid.</param>
/// <param name="Rotation">Camera rotation; present together with the position.</param>
/// <param name="TrailingNumber">
/// In a raid this is the raid clock in decimal hours (14.13 = 14:08). Menu screenshots can carry
/// a number too, whose meaning is not known, so it is only treated as a clock when a position is present.
/// </param>
public sealed record ScreenshotInfo(
    DateTime TakenAt,
    int Counter,
    WorldPoint? Position,
    Rotation? Rotation,
    double? TrailingNumber)
{
    public bool HasPosition => Position is not null;

    public double? RaidClockHours => HasPosition ? TrailingNumber : null;

    public double? YawDegrees => Rotation?.YawDegrees;
}

public static partial class ScreenshotName
{
    // 2026-01-01[13-35]_40.00, 2.50, 120.00_0.01000, 0.99900, -0.04000, 0.02000_14.13 (0).png
    // 2026-01-01[13-30]_11.72 (0).png
    // 2026-01-01[13-00] (0).png
    // The decimal separator is always '.', whatever the Windows locale.
    [GeneratedRegex(
        """
        ^(?<date>\d{4}-\d{2}-\d{2})\[(?<hh>\d{2})-(?<mm>\d{2})\]
        (?:_(?<x>-?\d+(?:\.\d+)?)\s*,\s*(?<y>-?\d+(?:\.\d+)?)\s*,\s*(?<z>-?\d+(?:\.\d+)?)
           (?:_(?<qx>-?\d+(?:\.\d+)?)\s*,\s*(?<qy>-?\d+(?:\.\d+)?)\s*,\s*(?<qz>-?\d+(?:\.\d+)?)\s*,\s*(?<qw>-?\d+(?:\.\d+)?))?
        )?
        (?:_(?<t>\d+(?:\.\d+)?))?
        \s*\((?<n>\d+)\)\.(?:png|jpe?g)$
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex Pattern();

    public static bool TryParse(string fileName, out ScreenshotInfo info)
    {
        info = null!;
        var match = Pattern().Match(Path.GetFileName(fileName));
        if (!match.Success)
            return false;

        if (!DateTime.TryParseExact(
                $"{match.Groups["date"].Value} {match.Groups["hh"].Value}:{match.Groups["mm"].Value}",
                "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var takenAt))
            return false;

        WorldPoint? position = null;
        if (match.Groups["x"].Success)
        {
            var p = new WorldPoint(Number(match, "x"), Number(match, "y"), Number(match, "z"));
            if (!IsPlausible(p.X) || !IsPlausible(p.Y) || !IsPlausible(p.Z))
                return false;
            position = p;
        }

        Rotation? rotation = null;
        if (match.Groups["qx"].Success)
        {
            var q = new Rotation(Number(match, "qx"), Number(match, "qy"), Number(match, "qz"), Number(match, "qw"));
            var norm = Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
            if (norm > 1e-4)
                rotation = new Rotation(q.X / norm, q.Y / norm, q.Z / norm, q.W / norm);
        }

        double? trailing = match.Groups["t"].Success ? Number(match, "t") : null;
        var counter = int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
        info = new ScreenshotInfo(takenAt, counter, position, rotation, trailing);
        return true;
    }

    private static double Number(Match m, string group) =>
        double.Parse(m.Groups[group].Value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static bool IsPlausible(double v) => double.IsFinite(v) && Math.Abs(v) < 100_000;
}
