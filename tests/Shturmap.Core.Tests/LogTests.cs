using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Raid;

namespace Shturmap.Core.Tests;

public class LogRecordReaderTests
{
    private const string Sample =
        "2026-01-01 12:00:00.201|1.1.0.1.46777|Info|push-notifications|Got notification | ChatMessageReceived\r\n" +
        "{\r\n" +
        "  \"type\": \"new_message\",\r\n" +
        "  \"eventId\": \"000000000000000000000042\",\r\n" +
        "  \"dialogId\": \"54cb50c76803fa8b248b4571\",\r\n" +
        "  \"message\": {\r\n" +
        "    \"type\": 10,\r\n" +
        "    \"dt\": 1767265200,\r\n" +
        "    \"text\": \"quest started\",\r\n" +
        "    \"templateId\": \"5936d90786f7742b1420ba5b description\"\r\n" +
        "  }\r\n" +
        "}\r\n" +
        "2026-01-01 12:00:00.201|1.1.0.1.46777|Info|push-notifications|NotificationManager.ProcessMessage | Received notification\r\n" +
        "2026-01-01 18:00:10.250|1.1.5.1.47510|Info|application|Session mode: Pve\r\n";

    [Fact]
    public void Same_records_whatever_the_chunking()
    {
        var whole = new LogRecordReader();
        var expected = whole.Append(Sample).ToList();
        expected.Add(whole.Flush()!);

        var chunked = new LogRecordReader();
        var actual = new List<LogRecord>();
        for (var i = 0; i < Sample.Length; i += 7)
            actual.AddRange(chunked.Append(Sample.Substring(i, Math.Min(7, Sample.Length - i))));
        actual.Add(chunked.Flush()!);

        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, actual);
        Assert.Contains("\"templateId\"", expected[0].Body);
        Assert.Equal("Session mode: Pve", expected[2].Message);
    }

    [Fact]
    public void Does_not_flush_a_half_written_json_block()
    {
        var reader = new LogRecordReader();
        reader.Append(Sample[..Sample.IndexOf("\"dt\"", StringComparison.Ordinal)]);
        Assert.Null(reader.Flush());
        Assert.NotNull(reader.Flush(force: true));
    }

    [Fact]
    public void Parses_a_quest_notification()
    {
        var reader = new LogRecordReader();
        var record = reader.Append(Sample)[0];
        var quest = Assert.IsType<QuestEvent>(GameLogParser.Parse(record));
        Assert.Equal("5936d90786f7742b1420ba5b", quest.QuestId);
        Assert.Equal(QuestLogStatus.Started, quest.Status);
        Assert.Equal("000000000000000000000042", quest.EventId);
        Assert.Equal("54cb50c76803fa8b248b4571", quest.TraderId);
    }
}

public class LogReplayTests
{
    private sealed record Raid(string? Scene, string? Location, RaidSide Side, GameMode Mode, DateTime? Started, DateTime Ended);

    private static (List<Raid> Raids, List<QuestEvent> Quests) Replay(string session)
    {
        var events = new List<GameEvent>();
        foreach (var file in Directory.GetFiles(Fixtures.PathTo("logs", session), "*.log"))
        {
            var reader = new LogRecordReader();
            var records = reader.Append(File.ReadAllText(file)).ToList();
            if (reader.Flush(force: true) is { } last)
                records.Add(last);
            events.AddRange(records.Select(GameLogParser.Parse).OfType<GameEvent>());
        }

        // Application and notification logs interleave by time; quest events carry server time, so order the
        // raid-relevant ones by log time and collect quests separately.
        var tracker = new RaidTracker();
        var raids = new List<Raid>();
        foreach (var e in events.Where(e => e is not QuestEvent).OrderBy(e => e.At))
        {
            if (tracker.Apply(e) is RaidEnded ended)
                raids.Add(new Raid(ended.Previous.ScenePath, ended.Previous.LocationId, ended.Previous.Side, ended.Previous.Mode,
                    ended.Previous.RaidStartedAt, ended.At));
        }
        return (raids, events.OfType<QuestEvent>().ToList());
    }

    [Fact]
    public void August_pvp_and_seasonal_session()
    {
        var (raids, quests) = Replay("log_2026.01.01_12-00-00_1.1.0.1.46777");

        Assert.Equal(
            new[]
            {
                ("maps/sandbox_start_preset.bundle", RaidSide.Unknown, true),
                ("maps/shopping_mall.bundle", RaidSide.Pmc, true),
                ("maps/customs_preset.bundle", RaidSide.Pmc, true),
                ("maps/shopping_mall.bundle", RaidSide.Unknown, false), // matching cancelled
                ("maps/shopping_mall.bundle", RaidSide.Scav, true),
                ("maps/customs_preset.bundle", RaidSide.Pmc, true),
                ("maps/customs_preset.bundle", RaidSide.Pmc, true),
                ("maps/shopping_mall.bundle", RaidSide.Scav, true),
            },
            raids.Select(r => (r.Scene!, r.Side, r.Started is not null)).ToArray());
        Assert.All(raids, r => Assert.Equal(GameMode.Seasonal, r.Mode));
        Assert.Equal("Interchange", raids[1].Location);

        Assert.Equal(8, quests.Count(q => q.Status == QuestLogStatus.Started));
        Assert.Equal(0, quests.Count(q => q.Status == QuestLogStatus.Completed));
    }

    [Fact]
    public void September_pve_streets_session()
    {
        var (raids, quests) = Replay("log_2026.01.01_15-00-00_1.1.5.1.47510");

        Assert.Equal(3, raids.Count);
        Assert.All(raids, r =>
        {
            Assert.Equal("maps/city_preset.bundle", r.Scene);
            Assert.Equal("TarkovStreets", r.Location);
            Assert.Equal(RaidSide.Pmc, r.Side);
            Assert.Equal(GameMode.Pve, r.Mode);
        });
        Assert.Equal("2026-01-01 16:27:14", raids[2].Started!.Value.ToString("yyyy-MM-dd HH:mm:ss"));

        Assert.Equal(2, quests.Count(q => q.Status == QuestLogStatus.Started));
        Assert.Equal(1, quests.Count(q => q.Status == QuestLogStatus.Completed));
    }

    [Fact]
    public void Locally_hosted_pve_raids_and_a_cancelled_queue()
    {
        var (raids, _) = Replay("log_2026.01.01_18-00-00_1.1.5.1.47510");
        Assert.Equal(new[] { false, true, true }, raids.Select(r => r.Started is not null).ToArray());
        Assert.All(raids, r => Assert.Equal("maps/customs_preset.bundle", r.Scene));
        Assert.All(raids.Where(r => r.Started is not null), r => Assert.Equal("bigmap", r.Location));

        var (woodsDay, completions) = Replay("log_2026.01.01_13-00-00_1.1.5.1.47510");
        Assert.Equal(new[] { "maps/customs_preset.bundle", "maps/woods_preset.bundle", "maps/woods_preset.bundle" },
            woodsDay.Select(r => r.Scene).ToArray());
        // Four completions: two catalog tasks (The Huntsman Path - Angry Watchman, Supply Plans) and two whose
        // template reads "<id> successMessageText <trader> 0" and match no catalog task (repeatable tasks).
        // The parser reports all of them; the quest engine ignores ids it does not know.
        Assert.Equal(
            new[] { "596a0e1686f7741ddf17dbee", "5d25e44386f77409453bce7b", "616041eb031af660100c9967", "61604635c725987e815b1a46" },
            completions.Where(q => q.Status == QuestLogStatus.Completed).Select(q => q.QuestId).Order(StringComparer.Ordinal).ToArray());
    }

    [Theory]
    // Real screenshots with their file times, and the map they must be attributed to.
    [InlineData("log_2026.01.01_13-00-00_1.1.5.1.47510", "2026-01-01 13:18:11", "customs")]
    [InlineData("log_2026.01.01_13-00-00_1.1.5.1.47510", "2026-01-01 13:18:40", "customs")]
    [InlineData("log_2026.01.01_13-00-00_1.1.5.1.47510", "2026-01-01 13:42:33", "woods")]
    [InlineData("log_2026.01.01_13-00-00_1.1.5.1.47510", "2026-01-01 14:13:28", "woods")]
    [InlineData("log_2026.01.01_15-00-00_1.1.5.1.47510", "2026-01-01 15:21:32", "streets-of-tarkov")]
    [InlineData("log_2026.01.01_15-00-00_1.1.5.1.47510", "2026-01-01 16:27:24", "streets-of-tarkov")]
    public void Screenshots_land_on_the_raid_they_were_taken_in(string session, string taken, string expectedMap)
    {
        var (raids, _) = Replay(session);
        var at = DateTime.Parse(taken, System.Globalization.CultureInfo.InvariantCulture);
        var raid = raids.Single(r => r.Started <= at && at <= r.Ended);
        var map = new MapResolver(Fixtures.MapIdentities).Resolve(raid.Scene, raid.Location);
        Assert.Equal(expectedMap, map?.NormalizedName);
    }

    [Theory]
    [InlineData("maps/city_preset.bundle", null, "streets-of-tarkov")]
    [InlineData("maps/shopping_mall.bundle", null, "interchange")]
    [InlineData("maps/sandbox_start_preset.bundle", null, "ground-zero-tutorial")]
    [InlineData("maps/factory_day_preset.bundle", null, "factory")]
    [InlineData(null, "TarkovStreets", "streets-of-tarkov")]
    [InlineData("maps/unknown_future_preset.bundle", "bigmap", "customs")]
    [InlineData("maps/city_v2_preset.bundle", null, null)]
    public void Resolves_maps_from_log_names(string? scene, string? location, string? expected)
    {
        Assert.Equal(expected, new MapResolver(Fixtures.MapIdentities).Resolve(scene, location)?.NormalizedName);
    }
}

// Group picks, loading steps and the insurer's notes (docs/NEXT.md items 1, 3 and 4), from line shapes seen in the
// owner's logs; ids are masked.
public class GroupLoadingInsuranceTests
{
    private const string Head = "|1.1.5.1.47510|Info|";

    private static List<GameEvent> Parse(string text)
    {
        var reader = new LogRecordReader();
        var records = reader.Append(text).ToList();
        if (reader.Flush(force: true) is { } last)
            records.Add(last);
        return records.Select(GameLogParser.Parse).OfType<GameEvent>().ToList();
    }

    private static string Push(string time, string kind, string body) =>
        $"2026-01-01 {time}{Head}push-notifications|Got notification | {kind}\r\n{body}\r\n" +
        $"2026-01-01 {time}{Head}push-notifications|NotificationManager.ProcessMessage | Received notification: Type: {kind}, Time: 0, Duration: Default, ShowNotification: False\r\n";

    private static string App(string time, string message) => $"2026-01-01 {time}{Head}application|{message}\r\n";

    [Fact]
    public void Reads_the_group_pick_and_status_but_not_twice()
    {
        var events = Parse(
            Push("12:30:00.100", "GroupMatchRaidSettings",
                "{\r\n  \"type\": \"groupMatchRaidSettings\",\r\n  \"eventId\": \"000000000000000000000000\",\r\n  \"raidSettings\": {\r\n" +
                "    \"location\": \"Sandbox_high\",\r\n    \"timeVariant\": \"PAST\",\r\n    \"raidMode\": \"Online\"\r\n  }\r\n}") +
            Push("12:30:00.200", "GroupMatchRaidReady", "{\r\n  \"type\": \"groupMatchRaidReady\"\r\n}") +
            Push("12:30:25.000", "GroupMatchStartGame", "{\r\n  \"type\": \"groupMatchStartGame\"\r\n}") +
            Push("12:58:00.000", "GroupMatchRaidNotReady", "{\r\n  \"type\": \"groupMatchRaidNotReady\"\r\n}"));

        var pick = Assert.IsType<GroupRaidSettingsEvent>(events[0]);
        Assert.Equal("Sandbox_high", pick.LocationId);
        Assert.Equal("PAST", pick.TimeVariant);
        Assert.Equal(
            new[] { GroupStatus.Ready, GroupStatus.Start, GroupStatus.NotReady },
            events.OfType<GroupStatusEvent>().Select(e => e.Status).ToArray());
        Assert.Equal(4, events.Count); // the "Received notification" lines add nothing
        Assert.Equal("ground-zero-21", new MapResolver(Fixtures.MapIdentities).Resolve(null, pick.LocationId)?.NormalizedName);
    }

    [Fact]
    public void Reads_the_loading_steps()
    {
        var events = Parse(
            App("13:00:00.000", "MatchingCompleted:0 real:0 diff:0") +
            App("13:00:30.000", "LocationLoaded:9.61 real:13.28 diff:3.67") +
            App("13:00:31.000", "GamePrepared:10.33 real:13.99 diff:3.66") +
            App("13:00:32.000", "GameCreated:10.83(0.5) real:14.5(0.51) diff:3.67") +
            App("13:00:38.000", "PlayerSpawnEvent:11.63(11.63) real:17.48(17.48) diff:5.84") +
            App("13:00:55.000", "GamePooled:14.23(3.39) real:20.86(6.36) diff:6.63") +
            App("13:01:13.000", "GameRunned:22.98(8.75) real:32.08(11.22) diff:9.1") +
            App("13:01:13.000", "GameSpawn:22.98(0) real:32.08(0) diff:9.1") +
            App("13:01:13.000", "GameSpawned:22.98(11.35) real:32.08(14.6) diff:9.1"));

        Assert.Equal(
            new[]
            {
                LoadingStep.MatchingCompleted, LoadingStep.LocationLoaded, LoadingStep.GamePrepared, LoadingStep.GameCreated,
                LoadingStep.PlayerSpawned, LoadingStep.GamePooled, LoadingStep.GameRunning,
            },
            events.Cast<LoadingStepEvent>().Select(e => e.Step).ToArray());
    }

    [Fact]
    public void Reads_the_insurers_notes()
    {
        const string lost =
            "{\r\n  \"type\": \"new_message\",\r\n  \"dialogId\": \"54cb50c76803fa8b248b4571\",\r\n  \"message\": {\r\n    \"type\": 2,\r\n" +
            "    \"dt\": 1767272400,\r\n    \"templateId\": \"000000000000000000000000 1\",\r\n    \"systemData\": {\r\n" +
            "      \"date\": \"01.01.2026\",\r\n      \"time\": \"16:00\",\r\n      \"location\": \"TarkovStreets\"\r\n    },\r\n" +
            "    \"hasRewards\": false\r\n  }\r\n}";
        const string returned =
            "{\r\n  \"type\": \"new_message\",\r\n  \"message\": {\r\n    \"type\": 8,\r\n    \"templateId\": \"000000000000000000000000 1\",\r\n" +
            "    \"systemData\": {\r\n      \"location\": \"bigmap\"\r\n    },\r\n    \"items\": {\r\n      \"stash\": \"s\",\r\n      \"data\": [\r\n" +
            "        { \"_id\": \"a\", \"parentId\": \"s\" },\r\n        { \"_id\": \"b\", \"parentId\": \"a\" },\r\n" +
            "        { \"_id\": \"c\", \"parentId\": \"s\" }\r\n      ]\r\n    },\r\n    \"hasRewards\": true\r\n  }\r\n}";
        var events = Parse(Push("14:00:00.000", "ChatMessageReceived", lost) + Push("23:00:00.000", "ChatMessageReceived", returned));

        var notes = events.Cast<InsuranceNoticeEvent>().ToList();
        Assert.Equal((InsuranceNotice.Lost, "TarkovStreets", 0), (notes[0].Kind, notes[0].LocationId, notes[0].ItemCount));
        Assert.Equal((InsuranceNotice.Returned, "bigmap", 2), (notes[1].Kind, notes[1].LocationId, notes[1].ItemCount)); // attachments don't count
    }

    [Fact]
    public void Tracks_the_loading_stage_and_its_timings()
    {
        var t0 = new DateTime(2026, 1, 1, 13, 0, 0);
        var tracker = new RaidTracker();
        tracker.Apply(new MapLoadingEvent(t0, "maps/customs_preset.bundle", null));
        Assert.Equal(("MAP", 0.1), Round(LoadingProgress.At(tracker.State, t0.AddSeconds(10))));
        Assert.Equal(("MAP", 0.225), Round(LoadingProgress.At(tracker.State, t0.AddSeconds(200)))); // never past 90 % of a stage

        tracker.Apply(new LoadingStepEvent(t0.AddSeconds(25), LoadingStep.LocationLoaded));
        tracker.Apply(new LoadingStepEvent(t0.AddSeconds(26), LoadingStep.GameCreated));
        Assert.Equal(LoadingStep.GameCreated, tracker.State.LoadingStep);
        Assert.Equal(t0.AddSeconds(25), tracker.State.LoadingStageSince); // the stage began with LocationLoaded
        Assert.Equal("RAID", LoadingProgress.At(tracker.State, t0.AddSeconds(30)).Stage);

        tracker.Apply(new LoadingStepEvent(t0.AddSeconds(42), LoadingStep.PlayerSpawned));
        tracker.Apply(new LoadingStepEvent(t0.AddSeconds(47), LoadingStep.GamePooled));
        Assert.Equal(("STARTING", 0.75), Round(LoadingProgress.At(tracker.State, t0.AddSeconds(47))));

        Assert.IsType<RaidStarted>(tracker.Apply(new GameStartedEvent(t0.AddSeconds(71))));
        Assert.Null(tracker.State.LoadingStep);
        Assert.Equal(
            new (LoadingStep?, double)[] { (LoadingStep.LocationLoaded, 25), (LoadingStep.GameCreated, 26), (LoadingStep.PlayerSpawned, 42), (LoadingStep.GamePooled, 47), (null, 71) },
            tracker.LoadingSteps.ToArray());
    }

    private static (string, double) Round((string Stage, double Fill) p) => (p.Stage, Math.Round(p.Fill, 3));

    [Fact]
    public void A_lost_note_during_the_raid_hints_how_it_ended()
    {
        // The Streets session: its third raid started 16:27:14 and its end line came at 16:32:38; the insurer's note
        // came 20 s before that.
        var events = new List<GameEvent>();
        foreach (var file in Directory.GetFiles(Fixtures.PathTo("logs", "log_2026.01.01_15-00-00_1.1.5.1.47510"), "*.log"))
            events.AddRange(Parse(File.ReadAllText(file)));
        var tracker = new RaidTracker();
        var hints = new RaidOutcomeHints();
        var found = new List<OutcomeHint>();
        var started = new List<IReadOnlyList<(LoadingStep? Step, double Seconds)>>();
        foreach (var e in events.Where(e => e is not QuestEvent).OrderBy(e => e.At))
        {
            if (e is InsuranceNoticeEvent notice && hints.Notice(notice, tracker.State, null) is { } late)
                found.Add(late);
            switch (tracker.Apply(e))
            {
                case RaidStarted:
                    started.Add(tracker.LoadingSteps);
                    break;
                case RaidEnded ended when hints.Ended(ended, null) is { } hint:
                    found.Add(hint);
                    break;
            }
        }

        var only = Assert.Single(found);
        Assert.Equal("TarkovStreets", only.LocationId);
        Assert.Equal("2026-01-01 16:32:38", only.RaidEndedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        Assert.InRange(only.NoticeSecondsFromEnd, -25, -15);
        // Every raid's loading logged its steps in order, ending with the raid start.
        Assert.Equal(3, started.Count);
        Assert.All(started, steps =>
        {
            Assert.Contains(steps, s => s.Step == LoadingStep.LocationLoaded);
            Assert.Null(steps[^1].Step);
            Assert.Equal(steps.Select(s => s.Seconds).Order().ToArray(), steps.Select(s => s.Seconds).ToArray());
        });
    }

    [Fact]
    public void A_late_note_counts_for_five_minutes_on_the_same_map_only()
    {
        var start = new DateTime(2026, 10, 1, 21, 0, 0);
        RaidEnded Raid(RaidTracker tracker, string location, DateTime at)
        {
            tracker.Apply(new MapLoadingEvent(at, "maps/customs_preset.bundle", null));
            tracker.Apply(new TransitInfoEvent(at.AddSeconds(30), "R", location));
            tracker.Apply(new GameStartedEvent(at.AddSeconds(60)));
            return (RaidEnded)tracker.Apply(new ProfileLoadedEvent(at.AddMinutes(20), "p"))!;
        }
        InsuranceNoticeEvent Lost(string location, DateTime at) => new(at, InsuranceNotice.Lost, location, 0);

        var tracker = new RaidTracker();
        var hints = new RaidOutcomeHints();
        var ended = Raid(tracker, "bigmap", start);
        Assert.Null(hints.Ended(ended, null));
        Assert.NotNull(hints.Notice(Lost("bigmap", ended.At.AddMinutes(2)), tracker.State, null));
        Assert.Null(hints.Notice(Lost("bigmap", ended.At.AddMinutes(3)), tracker.State, null)); // one hint per raid

        ended = Raid(tracker, "bigmap", start.AddHours(1));
        hints.Ended(ended, null);
        Assert.Null(hints.Notice(Lost("bigmap", ended.At.AddMinutes(6)), tracker.State, null)); // too late
        ended = Raid(tracker, "bigmap", start.AddHours(2));
        hints.Ended(ended, null);
        Assert.Null(hints.Notice(Lost("Woods", ended.At.AddMinutes(1)), tracker.State, null)); // another map
        Assert.Null(hints.Notice(new InsuranceNoticeEvent(ended.At.AddMinutes(1), InsuranceNotice.Returned, "bigmap", 3), tracker.State, null));

        // A load given up before the raid began gives no hint, even with a note.
        tracker.Apply(new MapLoadingEvent(start.AddHours(3), "maps/customs_preset.bundle", null));
        hints.Notice(Lost("bigmap", start.AddHours(3).AddSeconds(10)), tracker.State, null);
        Assert.Null(hints.Ended((RaidEnded)tracker.Apply(new MatchingCancelledEvent(start.AddHours(3).AddSeconds(20)))!, "bigmap"));
    }
}
