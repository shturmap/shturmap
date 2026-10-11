using Shturmap.Core.Logs;

namespace Shturmap.Core.Tests;

// The log isn't a documented format and patches change it: a record of a shape nobody expected is ignored, never
// a throw (one throw used to end the log following for the session; docs/NEXT.md, review of 2026-10-04, A9).
public class LogShapeTests
{
    private static readonly DateTime At = new(2026, 1, 1, 12, 30, 0);

    private static GameEvent? Push(string kind, string body) =>
        GameLogParser.Parse(new LogRecord(At, "1.1.5.1.47510", "Info", "push-notifications", "Got notification | " + kind, body));

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("12")]
    [InlineData("null")]
    [InlineData("{ not json")]
    public void A_body_that_is_no_object_is_ignored(string body)
    {
        Assert.Null(Push("ChatMessageReceived", body));
        Assert.Null(Push("GroupMatchRaidSettings", body));
        Assert.Null(Push("UserConfirmed", body));
    }

    [Theory]
    [InlineData("""{ "message": "text" }""")]
    [InlineData("""{ "message": null }""")]
    [InlineData("""{ "message": [ { "type": 10 } ] }""")]
    [InlineData("""{ "message": { "type": 10.5, "templateId": "5936d90786f7742b1420ba5b description" } }""")]
    [InlineData("""{ "message": { "type": "10", "templateId": "5936d90786f7742b1420ba5b description" } }""")]
    [InlineData("""{ "message": { "type": 99999999999, "templateId": "5936d90786f7742b1420ba5b description" } }""")]
    [InlineData("""{ "raidSettings": "TarkovStreets" }""")]
    [InlineData("""{ "raidSettings": null }""")]
    public void A_message_of_another_shape_is_ignored(string body)
    {
        Assert.Null(Push("ChatMessageReceived", body));
        Assert.Null(Push("GroupMatchRaidSettings", body));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"none\"")]
    [InlineData("[]")]
    [InlineData("""{ "data": null }""")]
    [InlineData("""{ "stash": 5, "data": "none" }""")]
    public void An_insurers_note_without_an_item_list_counts_no_items(string items)
    {
        var note = Assert.IsType<InsuranceNoticeEvent>(Push("ChatMessageReceived",
            $$"""{ "message": { "type": 8, "systemData": { "location": "bigmap" }, "items": {{items}} } }"""));
        Assert.Equal((InsuranceNotice.Returned, "bigmap", 0), (note.Kind, note.LocationId, note.ItemCount));
        Assert.NotNull(note.Items);
        Assert.Empty(note.Items);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"none\"")]
    [InlineData("[]")]
    [InlineData("""{ "data": null }""")]
    [InlineData("""{ "stash": 5, "data": "none" }""")]
    [InlineData("""{ "stash": "s", "data": { "_tpl": "000000000000000000000001" } }""")]
    public void A_quest_start_without_an_item_list_hands_nothing_over(string items)
    {
        var quest = Assert.IsType<QuestEvent>(Push("ChatMessageReceived",
            $$"""{ "message": { "type": 10, "templateId": "5936d90786f7742b1420ba5b description", "items": {{items}} } }"""));
        Assert.Equal(QuestLogStatus.Started, quest.Status);
        Assert.NotNull(quest.Items);
        Assert.Empty(quest.Items);
    }

    [Fact]
    public void An_item_entry_of_another_shape_is_counted_but_not_named_and_an_odd_count_is_one()
    {
        // Entries that are no object are neither counted nor named; one without a usable template id is counted
        // only; a stack count that isn't a whole positive number is one item.
        const string data = """
            [
              1, "x", null, [],
              { "parentId": "s" },
              { "parentId": "s", "_tpl": 5 },
              { "parentId": "s", "_tpl": "" },
              { "parentId": "s", "_tpl": null },
              { "parentId": "s", "_tpl": "000000000000000000000001", "upd": "3" },
              { "parentId": "s", "_tpl": "000000000000000000000002", "upd": null },
              { "parentId": "s", "_tpl": "000000000000000000000003", "upd": { "StackObjectsCount": "3" } },
              { "parentId": "s", "_tpl": "000000000000000000000004", "upd": { "StackObjectsCount": 2.5 } },
              { "parentId": "s", "_tpl": "000000000000000000000005", "upd": { "StackObjectsCount": 0 } },
              { "parentId": "s", "_tpl": "000000000000000000000006", "upd": { "StackObjectsCount": -2 } },
              { "parentId": "s", "_tpl": "000000000000000000000007", "upd": { "StackObjectsCount": 99999999999 } },
              { "parentId": "s", "_tpl": "000000000000000000000008", "upd": { "StackObjectsCount": 4 } },
              { "parentId": 7, "_tpl": "000000000000000000000009" },
              { "_tpl": "000000000000000000000009" }
            ]
            """;
        var note = Assert.IsType<InsuranceNoticeEvent>(Push("ChatMessageReceived",
            $$"""{ "message": { "type": 8, "systemData": { "location": "bigmap" }, "items": { "stash": "s", "data": {{data}} } } }"""));

        Assert.Equal(12, note.ItemCount);
        Assert.Equal(
            new[]
            {
                new ItemGrant("000000000000000000000001", 1), new ItemGrant("000000000000000000000002", 1), new ItemGrant("000000000000000000000003", 1),
                new ItemGrant("000000000000000000000004", 1), new ItemGrant("000000000000000000000005", 1), new ItemGrant("000000000000000000000006", 1),
                new ItemGrant("000000000000000000000007", 1), new ItemGrant("000000000000000000000008", 4),
            },
            note.Items);
    }

    [Fact]
    public void An_insurers_note_with_odd_system_data_is_ignored()
    {
        Assert.Null(Push("ChatMessageReceived", """{ "message": { "type": 2, "systemData": "bigmap" } }"""));
        Assert.Null(Push("ChatMessageReceived", """{ "message": { "type": 2, "systemData": { "location": 7 } } }"""));
    }

    [Theory]
    [InlineData("1767265200", true)] // the server's time: 2026-01-01 11:00:00 UTC
    [InlineData("1767265200.75", true)] // a fraction is cut
    [InlineData("\"1767265200\"", false)]
    [InlineData("99999999999999999", false)] // no time a date can hold
    [InlineData("1e300", false)]
    [InlineData("-1e300", false)]
    [InlineData("null", false)]
    public void A_quests_time_is_the_servers_when_it_is_a_time_and_the_lines_otherwise(string dt, bool fromServer)
    {
        var quest = Assert.IsType<QuestEvent>(Push("ChatMessageReceived",
            $$"""{ "eventId": "e", "message": { "type": 10, "dt": {{dt}}, "templateId": "5936d90786f7742b1420ba5b description" } }"""));
        Assert.Equal(fromServer ? DateTimeOffset.FromUnixTimeSeconds(1767265200).LocalDateTime : At, quest.At);
        Assert.Equal(QuestLogStatus.Started, quest.Status);
    }

    [Fact]
    public void A_match_setup_without_its_fields_still_counts()
    {
        var setup = Assert.IsType<MatchSetupEvent>(Push("UserConfirmed", """{ "location": 5, "profileid": null }"""));
        Assert.Null(setup.LocationId);
        Assert.Null(setup.ProfileId);
    }

    [Fact]
    public void Digits_in_a_headers_shape_that_are_no_time_are_not_a_header()
    {
        var reader = new LogRecordReader();
        var records = reader.Append(
            "2026-01-01 18:00:10.250|1.1.5.1.47510|Info|application|Session mode: Pve\r\n" +
            "2026-13-45 99:99:99.999|1.1.5.1.47510|Info|application|scene preset path:maps/city_preset.bundle\r\n" +
            "2026-01-01 18:00:11.000|1.1.5.1.47510|Info|application|GameStarted:1(1) real:1(1) diff:0\r\n").ToList();
        records.Add(reader.Flush(force: true)!);

        Assert.Equal(2, records.Count);
        Assert.IsType<SessionModeEvent>(GameLogParser.Parse(records[0]));
        Assert.Contains("2026-13-45", records[0].Body); // kept as the line before's continuation
        Assert.IsType<GameStartedEvent>(GameLogParser.Parse(records[1]));
    }
}
