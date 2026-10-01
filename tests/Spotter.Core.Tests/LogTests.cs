using Spotter.Core.Logs;
using Spotter.Core.Maps;
using Spotter.Core.Raid;

namespace Spotter.Core.Tests;

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
        Assert.Equal(QuestStatus.Started, quest.Status);
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

        Assert.Equal(8, quests.Count(q => q.Status == QuestStatus.Started));
        Assert.Equal(0, quests.Count(q => q.Status == QuestStatus.Completed));
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

        Assert.Equal(2, quests.Count(q => q.Status == QuestStatus.Started));
        Assert.Equal(1, quests.Count(q => q.Status == QuestStatus.Completed));
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
            completions.Where(q => q.Status == QuestStatus.Completed).Select(q => q.QuestId).Order(StringComparer.Ordinal).ToArray());
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
