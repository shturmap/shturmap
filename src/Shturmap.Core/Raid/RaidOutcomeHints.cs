using Shturmap.Core.Logs;

namespace Shturmap.Core.Raid;

/// <summary>The insurer wrote that insured gear was lost in this raid: a hint of how it ended, never a fact.</summary>
/// <param name="NoticeSecondsFromEnd">When the note came, relative to the raid's end line (negative: before it).</param>
public sealed record OutcomeHint(string LocationId, DateTime RaidEndedAt, double NoticeSecondsFromEnd);

/// <summary>
/// Pairs the insurer's "lost" note with the raid it is about, for the study log only (docs/DESIGN.md §8). In the
/// owner's logs the note came 17–20 s before the raid's end line, so it counts while the raid runs and up to five
/// minutes after it ended, on the same location. No note proves nothing: the gear may not have been insured.
/// </summary>
public sealed class RaidOutcomeHints
{
    public static readonly TimeSpan LateBy = TimeSpan.FromMinutes(5);

    private InsuranceNoticeEvent? _lost;
    private (string? LocationId, string? MapNameId, DateTime EndedAt)? _ended;

    /// <summary>A notice arrived. Returns a hint when it belongs to a raid that has already ended.</summary>
    /// <param name="mapNameId">The shown map's nameId, for raids whose location the log didn't name.</param>
    public OutcomeHint? Notice(InsuranceNoticeEvent notice, RaidState raid, string? mapNameId)
    {
        if (notice.Kind != InsuranceNotice.Lost)
            return null;
        if (raid.Phase == RaidPhase.InRaid && Same(notice.LocationId, raid.LocationId, mapNameId))
        {
            _lost = notice;
            return null;
        }
        if (_ended is { } ended && notice.At >= ended.EndedAt && notice.At - ended.EndedAt <= LateBy &&
            Same(notice.LocationId, ended.LocationId, ended.MapNameId))
        {
            _ended = null;
            return new OutcomeHint(notice.LocationId, ended.EndedAt, (notice.At - ended.EndedAt).TotalSeconds);
        }
        return null;
    }

    /// <summary>A raid ended (or its loading was given up). Returns a hint when the note came during the raid.</summary>
    public OutcomeHint? Ended(RaidEnded ended, string? mapNameId)
    {
        var lost = _lost;
        _lost = null;
        _ended = null;
        if (ended.Previous.RaidStartedAt is null)
            return null;
        if (lost is not null && Same(lost.LocationId, ended.Previous.LocationId, mapNameId))
            return new OutcomeHint(lost.LocationId, ended.At, (lost.At - ended.At).TotalSeconds);
        _ended = (ended.Previous.LocationId, mapNameId, ended.At);
        return null;
    }

    private static bool Same(string location, string? raidLocation, string? mapNameId) =>
        string.Equals(location, raidLocation, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(location, mapNameId, StringComparison.OrdinalIgnoreCase);
}
