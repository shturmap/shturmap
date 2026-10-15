using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// Notes on a quest (owner, 2026-10-15): the player's own words, one set per quest and game mode, kept in notes.json,
// each with when it was written; added and deleted, never edited; listed newest first; never in a report, the
// diagnostics or the study log's text.
public sealed class QuestNotesTests : IDisposable
{
    private const string Quest = "aaaaaaaaaaaaaaaaaaaaaa21", Other = "aaaaaaaaaaaaaaaaaaaaaa22";

    private static readonly TimeSpan Cet = TimeSpan.FromHours(1);
    private static readonly DateTimeOffset Morning = new(2026, 1, 1, 9, 5, 0, Cet);

    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-notes-").FullName;
    private readonly List<string> _warnings = [];

    public void Dispose() => Directory.Delete(_root, true);

    private string NotesFile => Path.Combine(_root, QuestNotes.FileName);

    private QuestNotes Load() => QuestNotes.Load(NotesFile, _warnings.Add);

    // ---- the store ----

    [Fact]
    public void Notes_are_kept_in_the_file_with_when_they_were_written()
    {
        var notes = Load();
        Assert.Empty(notes.For(GameMode.Pve, Quest));
        Assert.False(File.Exists(NotesFile));

        // To the second, with the offset of the moment.
        var first = notes.Add(GameMode.Pve, Quest, "Bring the key to the dorm", Morning.AddMilliseconds(250));
        Assert.Equal(Morning, first.At);
        Assert.Equal(8, first.Id.Length);
        var second = notes.Add(GameMode.Pvp, Other, "Grüße an Prapor", Morning.AddHours(6));
        notes.Save();

        var json = File.ReadAllText(NotesFile);
        Assert.Contains("\"notes\"", json);
        Assert.Contains("\"mode\": \"pve\"", json);
        Assert.Contains("\"mode\": \"pvp\"", json);
        Assert.Contains($"\"quest\": \"{Quest}\"", json);
        Assert.Contains("\"at\": \"2026-01-01T09:05:00+01:00\"", json);
        // The words as written, readable in the file.
        Assert.Contains("\"text\": \"Grüße an Prapor\"", json);

        var again = Load();
        Assert.Equal([first], again.For(GameMode.Pve, Quest));
        Assert.Equal([second], again.For(GameMode.Pvp, Other));
        Assert.Equal(Cet, again.For(GameMode.Pve, Quest)[0].At.Offset);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void The_newest_comes_first()
    {
        var notes = Load();
        var early = notes.Add(GameMode.Pve, Quest, "early", Morning);
        var late = notes.Add(GameMode.Pve, Quest, "late", Morning.AddMinutes(15));
        // Written after the late one, stamped before it (the clock was set back): the stamp decides.
        var middle = notes.Add(GameMode.Pve, Quest, "middle", Morning.AddMinutes(5));
        // Two in the same second: the one added later comes first.
        var sameSecond = notes.Add(GameMode.Pve, Quest, "same second", Morning.AddMinutes(15).AddMilliseconds(500));
        Assert.Equal([sameSecond, late, middle, early], notes.For(GameMode.Pve, Quest));
        Assert.Equal([sameSecond, late, middle, early], notes.Of(GameMode.Pve)[Quest]);

        notes.Save();
        Assert.Equal([sameSecond, late, middle, early], Load().For(GameMode.Pve, Quest));
    }

    [Fact]
    public void Each_quest_and_each_mode_has_its_own() // PvE and PvP progress are separate, as ticks and picks are
    {
        var notes = Load();
        var pve = notes.Add(GameMode.Pve, Quest, "in PvE", Morning);
        var pvp = notes.Add(GameMode.Pvp, Quest, "in PvP", Morning);
        var other = notes.Add(GameMode.Pve, Other, "another quest", Morning);

        Assert.Equal([pve], notes.For(GameMode.Pve, Quest));
        Assert.Equal([pvp], notes.For(GameMode.Pvp, Quest));
        Assert.Equal([other], notes.For(GameMode.Pve, Other));
        Assert.Empty(notes.For(GameMode.Pvp, Other));
        Assert.Equal([Quest, Other], notes.Of(GameMode.Pve).Keys.Order());
        Assert.Equal([Quest], notes.Of(GameMode.Pvp).Keys);
        Assert.Empty(notes.Of(GameMode.Seasonal));
    }

    [Fact]
    public void A_note_is_deleted_by_its_id_and_the_file_follows()
    {
        var notes = Load();
        var keep = notes.Add(GameMode.Pve, Quest, "keep", Morning);
        var gone = notes.Add(GameMode.Pvp, Quest, "gone", Morning.AddMinutes(1));
        notes.Save();
        var before = notes.Of(GameMode.Pvp);

        Assert.Equal(Quest, notes.QuestOf(gone.Id));
        Assert.True(notes.Delete(gone.Id));
        Assert.False(notes.Delete(gone.Id));
        Assert.False(notes.Delete("no-such-note"));
        Assert.Null(notes.QuestOf(gone.Id));
        Assert.Empty(notes.For(GameMode.Pvp, Quest));
        // A snapshot's set stays as it was: the change makes a new one.
        Assert.Equal([gone], before[Quest]);
        notes.Save();

        var again = Load();
        Assert.Equal([keep], again.For(GameMode.Pve, Quest));
        Assert.Empty(again.For(GameMode.Pvp, Quest));
        Assert.DoesNotContain("gone", File.ReadAllText(NotesFile));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" \r\n\t \r\n ")]
    public void An_empty_note_is_refused(string text)
    {
        var notes = Load();
        Assert.Throws<ArgumentException>(() => notes.Add(GameMode.Pve, Quest, text, Morning));
        Assert.Empty(notes.For(GameMode.Pve, Quest));
    }

    [Fact]
    public void The_words_are_trimmed_and_their_line_breaks_kept()
    {
        var notes = Load();
        Assert.Equal("Bring the key\nfor room 114", notes.Add(GameMode.Pve, Quest, "  \r\n Bring the key\r\nfor room 114 \r\n ", Morning).Text);
        // WinUI's text box breaks lines with "\r": kept as a line break all the same.
        Assert.Equal("one\ntwo\n\nthree", notes.Add(GameMode.Pve, Quest, "one\rtwo\r\rthree", Morning).Text);
    }

    [Fact]
    public void A_long_note_is_cut_at_500_characters()
    {
        var notes = Load();
        Assert.Equal(new string('a', 500), notes.Add(GameMode.Pve, Quest, new string('a', 500), Morning).Text);
        Assert.Equal(new string('b', 500), notes.Add(GameMode.Pve, Quest, new string('b', 600), Morning).Text);
        // Counted after the trim.
        Assert.Equal(new string('c', 500), notes.Add(GameMode.Pve, Quest, "   " + new string('c', 500) + "   ", Morning).Text);
        // Never between the halves of an emoji: one that would be cut goes whole.
        var emoji = notes.Add(GameMode.Pve, Quest, new string('d', 499) + "\U0001F600 and more", Morning).Text;
        Assert.Equal(new string('d', 499), emoji);
        // And no space left at the end of the cut.
        Assert.Equal(new string('e', 498), notes.Add(GameMode.Pve, Quest, new string('e', 498) + "  more words", Morning).Text);
    }

    [Fact]
    public void A_file_that_cant_be_read_is_set_aside_and_the_notes_start_over()
    {
        File.WriteAllText(NotesFile, "{ \"notes\": [ { \"id\": ");
        var notes = Load();
        Assert.Empty(notes.Of(GameMode.Pve));
        var warning = Assert.Single(_warnings);
        Assert.Contains("set aside as notes.json.unreadable-", warning);
        Assert.False(File.Exists(NotesFile));
        var aside = Assert.Single(Directory.GetFiles(_root, QuestNotes.FileName + ".unreadable-*"));
        Assert.Equal("{ \"notes\": [ { \"id\": ", File.ReadAllText(aside));

        // A fresh file from the next note on.
        var note = notes.Add(GameMode.Pve, Quest, "after", Morning);
        notes.Save();
        Assert.Equal([note], Load().For(GameMode.Pve, Quest));

        // A second file that can't be read never goes over the first one set aside: its notes were the player's.
        File.WriteAllText(NotesFile, "not JSON at all");
        Load();
        Assert.Equal(2, Directory.GetFiles(_root, QuestNotes.FileName + ".unreadable-*").Length);
        Assert.Equal("{ \"notes\": [ { \"id\": ", File.ReadAllText(aside));
    }

    [Fact]
    public void A_file_out_of_reach_for_now_is_left_as_it_is_and_never_written_over()
    {
        var first = Load();
        first.Add(GameMode.Pve, Quest, "kept", Morning);
        first.Save();
        var json = File.ReadAllText(NotesFile);

        QuestNotes notes;
        using (new FileStream(NotesFile, FileMode.Open, FileAccess.Read, FileShare.None))
            notes = Load();
        Assert.Contains("couldn't be opened", Assert.Single(_warnings));
        notes.Add(GameMode.Pve, Quest, "this run only", Morning);
        notes.Save();
        Assert.Equal(json, File.ReadAllText(NotesFile));
        Assert.Empty(Directory.GetFiles(_root, QuestNotes.FileName + ".unreadable-*"));
    }

    [Fact]
    public void A_note_it_cant_place_is_left_out_and_said_so()
    {
        File.WriteAllText(NotesFile, $$"""
            {
              "version": 1,
              "notes": [
                { "id": "aaaa0001", "mode": "pve", "quest": "{{Quest}}", "at": "2026-01-01T09:05:00+01:00", "text": "fine" },
                { "id": "aaaa0002", "mode": "arena", "quest": "{{Quest}}", "at": "2026-01-01T09:06:00+01:00", "text": "no such mode" },
                { "id": "aaaa0003", "mode": "pve", "at": "2026-01-01T09:07:00+01:00", "text": "no quest" },
                { "id": "aaaa0001", "mode": "pve", "quest": "{{Quest}}", "at": "2026-01-01T09:08:00+01:00", "text": "its id is taken" },
                { "id": "aaaa0004", "mode": "pve", "quest": "{{Quest}}", "at": "2026-01-01T09:09:00+01:00" }
              ]
            }
            """);
        var note = Assert.Single(Load().For(GameMode.Pve, Quest));
        Assert.Equal(new QuestNote("aaaa0001", Morning, "fine"), note);
        Assert.Contains("4 notes", Assert.Single(_warnings));
    }

    // ---- the card ----

    [Fact]
    public void The_quest_card_carries_its_quests_notes_newest_first()
    {
        var notes = Load();
        var early = notes.Add(GameMode.Pve, Quest, "early", Morning);
        var late = notes.Add(GameMode.Pve, Quest, "late", Morning.AddHours(1));
        notes.Add(GameMode.Pve, Other, "another quest's", Morning);
        var data = Data(GameMode.Pve);
        var quests = new Dictionary<string, Core.Quests.QuestStatus>();

        Assert.Equal([late, early], QuestCards.Build(data, quests, Quest, notes: notes.Of(GameMode.Pve))!.Notes);
        Assert.Empty(QuestCards.Build(data, quests, Quest, notes: notes.Of(GameMode.Pvp))!.Notes);
        Assert.Empty(QuestCards.Build(data, quests, Quest)!.Notes);
    }

    // A developer script's note (its "note" step), as its ticks: on the card for the run, never in the file, so a
    // script run on the developer folder leaves no notes behind.
    [Fact]
    public void A_note_for_the_session_only_is_listed_and_never_written()
    {
        var notes = Load();
        var kept = notes.Add(GameMode.Pve, Quest, "kept", Morning);
        var scripted = notes.Add(GameMode.Pve, Quest, "for a snapshot", Morning.AddMinutes(1), save: false);
        Assert.Equal([scripted, kept], notes.For(GameMode.Pve, Quest));
        notes.Save();
        Assert.DoesNotContain("for a snapshot", File.ReadAllText(NotesFile));
        Assert.Equal([kept], Load().For(GameMode.Pve, Quest));
    }

    // ---- the session ----

    private static GameData Data(GameMode mode)
    {
        var data = SessionRig.Data(mode);
        return new GameData
        {
            Mode = data.Mode,
            Language = data.Language,
            Maps = data.Maps,
            Tasks = new Dictionary<string, ApiTask>
            {
                [Quest] = new(Quest, "A quest", null, null, null, null, false, false, null, null, null, false, null, [], null),
                [Other] = new(Other, "Another quest", null, null, null, null, false, false, null, null, null, false, null, [], null),
            },
            Traders = data.Traders,
            MapDefinitions = data.MapDefinitions,
            CheckedAt = data.CheckedAt,
        };
    }

    private const string Words = "Meet at the old gas station with the flare";

    [Fact]
    public async Task A_note_shows_on_its_quests_card_stays_across_a_restart_and_goes_when_deleted()
    {
        var clock = new DateTime(2026, 1, 1, 15, 40, 12);
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations) { GivenData = Data, Clock = () => clock });
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Mode == GameMode.Pve, "the data in PvE");
        Assert.Empty(rig.Snapshot.QuestNotes);

        await rig.Session.AddQuestNoteAsync(Quest, "  " + Words + "\r\nand the key  ");
        var s = rig.Snapshot;
        var note = Assert.Single(s.QuestNotes[Quest]);
        Assert.Equal(Words + "\nand the key", note.Text);
        Assert.Equal(new DateTimeOffset(clock), note.At);
        Assert.Equal([note], QuestCards.Build(s.Data!, s.Quests, Quest, notes: s.QuestNotes)!.Notes);
        Assert.False(s.QuestNotes.ContainsKey(Other));
        // The file has it before the snapshot shows it.
        Assert.Contains(note.Id, File.ReadAllText(Path.Combine(rig.AppFolder, QuestNotes.FileName)));
        // Nothing to keep: no note, and no word of it anywhere.
        await rig.Session.AddQuestNoteAsync(Quest, " \r\n ");
        await rig.Session.AddQuestNoteAsync(Other, "");
        Assert.Equal([note], rig.Snapshot.QuestNotes[Quest]);
        Assert.False(rig.Snapshot.QuestNotes.ContainsKey(Other));

        // Never in the diagnostics.
        var diagnostics = Diagnostics.Build(rig.Snapshot, "0.5.0", "Windows 11 (10.0.26200)", "dev build", [], clock, null);
        Assert.DoesNotContain("gas station", diagnostics);

        // PvP has its own notes: none here.
        rig.Log("Session mode: Regular");
        await rig.Until(s => s.Mode == GameMode.Pvp, "PvP");
        Assert.Empty(rig.Snapshot.QuestNotes);

        // Kept across a restart, in PvE.
        await rig.RestartAsync();
        await rig.StartAsync();
        rig.Log("Session mode: Pve");
        var again = await rig.Until(s => s.Mode == GameMode.Pve && s.QuestNotes.ContainsKey(Quest), "the note in PvE after a restart");
        Assert.Equal([note], again.QuestNotes[Quest]);

        await rig.Session.DeleteQuestNoteAsync(note.Id);
        s = rig.Snapshot;
        Assert.Empty(s.QuestNotes);
        Assert.Empty(QuestCards.Build(s.Data!, s.Quests, Quest, notes: s.QuestNotes)!.Notes);
        // Deleted already: nothing happens.
        await rig.Session.DeleteQuestNoteAsync(note.Id);
        await rig.RestartAsync();
        await rig.StartAsync();
        Assert.DoesNotContain("gas station", File.ReadAllText(Path.Combine(rig.AppFolder, QuestNotes.FileName)));
        Assert.Empty(rig.Snapshot.QuestNotes);

        // The study log counts the notes and names the quest, never the words (developer builds; a release has none).
        var study = Path.Combine(rig.AppFolder, "study");
        var lines = Directory.Exists(study)
            ? Directory.GetFiles(study, "*.jsonl").SelectMany(f => ReadShared(f).Split('\n')).ToList()
            : [];
        Assert.DoesNotContain(lines, l => l.Contains("gas station", StringComparison.Ordinal));
        if (StudyLog.Available)
        {
            var added = Assert.Single(lines, l => l.Contains("\"ev\":\"note.add\"", StringComparison.Ordinal));
            Assert.Contains($"\"quest\":\"{Quest}\"", added);
            Assert.Contains($"\"length\":{note.Text.Length}", added);
            Assert.Contains($"\"quest\":\"{Quest}\"", Assert.Single(lines, l => l.Contains("\"ev\":\"note.delete\"", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public async Task A_scripts_note_shows_on_the_card_and_writes_no_file()
    {
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations) { GivenData = Data });
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Mode == GameMode.Pve, "the data in PvE");

        await rig.Session.AddQuestNoteAsync(Quest, Words, save: false);
        var s = rig.Snapshot;
        Assert.Equal(Words, Assert.Single(QuestCards.Build(s.Data!, s.Quests, Quest, notes: s.QuestNotes)!.Notes).Text);
        Assert.False(File.Exists(Path.Combine(rig.AppFolder, QuestNotes.FileName)));
    }

    // The study log is held open by the running session.
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

// When a note was written, as its card says it (QuestCards.NoteStamp; owner, 2026-10-15): the day and
// the time, in this PC's time, as the language in use writes them, as the tick's day and the stash's marks are.
[Collection("UiLanguage")]
public sealed class QuestNoteStampTests
{
    // 15:40 on the examples' day in this PC's time zone, whichever it is: the stamp is read in the PC's time.
    private static readonly DateTimeOffset Written = new(new DateTime(2026, 1, 1, 15, 40, 12, DateTimeKind.Local));

    [Fact]
    public void A_notes_stamp_is_its_day_and_time_as_the_language_in_use_writes_them()
    {
        try
        {
            Assert.Equal("1 Jan 15:40", QuestCards.NoteStamp(Written));
            UiLanguage.Set(UiLanguage.German);
            Assert.Equal("1. Jan., 15:40", QuestCards.NoteStamp(Written));
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }

    // The moment counts, not the offset it was written with: the same moment is the same stamp.
    [Fact]
    public void The_same_moment_under_another_offset_is_the_same_stamp() =>
        Assert.Equal(QuestCards.NoteStamp(new DateTimeOffset(2026, 1, 1, 15, 40, 0, TimeSpan.FromHours(1))),
            QuestCards.NoteStamp(new DateTimeOffset(2026, 1, 1, 14, 40, 0, TimeSpan.Zero)));
}
