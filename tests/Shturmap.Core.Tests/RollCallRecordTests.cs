using Shturmap.App.Rules;
using Shturmap.Core.Raid;

namespace Shturmap.Core.Tests;

/// <summary>
/// "A roll call ran this raid" is the window's, kept for the raid and its map (coordinator, 2026-10-15): a look at another
/// map in the raid and back made the raid map's scene anew, and the scene's record went with it, so the next list got a
/// whole roll call again. A new raid, or a transit to another map, starts without one.
/// </summary>
public class RollCallRecordTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    private static RaidState InRaid(string? id = "raid-1", DateTime? started = null) =>
        new() { Phase = RaidPhase.InRaid, RaidId = id, RaidStartedAt = started ?? Start };

    [Fact]
    public void A_look_at_another_map_and_back_keeps_what_the_roll_call_called()
    {
        var record = new RollCallRecord();
        var raid = RollCallRecord.Of("customs", InRaid());
        Assert.Null(record.In(raid));
        record.Called(raid!.Value, [("extract:crossroads", true), ("extract:zb-1013", false)]);
        // Woods looked at, Customs back: the raid's key is the raid's map, whatever is on screen, so it is the same.
        var back = RollCallRecord.Of("customs", InRaid());
        Assert.Equal(new Dictionary<string, bool> { ["extract:crossroads"] = true, ["extract:zb-1013"] = false }, record.In(back));
        // A later list's additions join it; one marked "??:??:??" before and lit now is lit.
        record.Called(back!.Value, [("extract:zb-1013", true), ("extract:trailer-park", true)]);
        Assert.Equal(3, record.In(back)!.Count);
        Assert.True(record.In(back)!["extract:zb-1013"]);
    }

    [Fact]
    public void A_new_raid_or_a_transit_to_another_map_starts_without_one()
    {
        var record = new RollCallRecord();
        record.Called(RollCallRecord.Of("customs", InRaid())!.Value, [("extract:crossroads", true)]);
        // The next raid on the same map: another start, another id.
        Assert.Null(record.In(RollCallRecord.Of("customs", InRaid("raid-2", Start.AddMinutes(50)))));
        // A local raid without an id, started later: another raid too.
        Assert.Null(record.In(RollCallRecord.Of("customs", InRaid(null, Start.AddMinutes(50)))));
        // A transit to Interchange: the raid's map changed.
        Assert.Null(record.In(RollCallRecord.Of("interchange", InRaid())));
        // Recording the new raid forgets the last one's.
        var next = RollCallRecord.Of("customs", InRaid("raid-2", Start.AddMinutes(50)))!.Value;
        record.Called(next, [("extract:trailer-park", false)]);
        Assert.Null(record.In(RollCallRecord.Of("customs", InRaid())));
        Assert.Equal(["extract:trailer-park"], record.In(next)!.Keys);
    }

    [Fact]
    public void Outside_a_raid_there_is_none()
    {
        Assert.Null(RollCallRecord.Of("customs", new RaidState { Phase = RaidPhase.Menu }));
        Assert.Null(RollCallRecord.Of("customs", new RaidState { Phase = RaidPhase.Loading, RaidId = "raid-1" }));
        Assert.Null(RollCallRecord.Of(null, InRaid()));
        Assert.Null(new RollCallRecord().In(null));
    }
}
